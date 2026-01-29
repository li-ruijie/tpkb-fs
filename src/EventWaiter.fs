/// Asynchronous event waiter for detecting LR (left-right) trigger patterns.
///
/// Uses a synchronous queue and background thread to wait for the second button
/// press within the poll timeout, enabling detection of simultaneous button presses.
///
/// State machine for LR trigger:
/// 1. First button down → start() → enters waiting state
/// 2. Waiting for second button (poll timeout)
///    - Move event → resend first down, exit waiting
///    - First up → resend click, exit waiting
///    - Second down → enter scroll mode, exit waiting
///    - Timeout → resend first down, exit waiting
///
/// This allows distinguishing between:
/// - L+R simultaneous press (scroll trigger)
/// - L then R sequential press (two separate clicks)
/// - L click (single button press and release)
module EventWaiter

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Threading

open Mouse

type private SynchronousQueue() =
    let event = new ManualResetEventSlim(false)
    let offerDone = new ManualResetEventSlim(false)

    // 0=Idle, 1=Waiting, 2=Offered, 3=Done
    let mutable state = 0
    let mutable slot = NonEvent

    member self.setWaiting () =
        event.Reset()
        Interlocked.Exchange(&state, 1) |> ignore

    member self.poll (timeout: int): MouseEvent option =
        if event.Wait(timeout) then
            // Signaled: producer wrote slot and set state to OFFERED (2)
            let res = slot
            if Interlocked.CompareExchange(&state, 3, 2) = 2 then
                offerDone.Set()
                Some(res)
            else
                // Unexpected state after signal; fall through to timeout path
                if Interlocked.CompareExchange(&state, 0, 1) = 1 then
                    None
                else
                    let res = slot
                    Interlocked.Exchange(&state, 3) |> ignore
                    offerDone.Set()
                    Some(res)
        else
            // Timed out: try WAITING -> IDLE
            if Interlocked.CompareExchange(&state, 0, 1) = 1 then
                None
            else
                // Lost race: producer set OFFERED after timeout, safe to read slot
                let res = slot
                Interlocked.Exchange(&state, 3) |> ignore
                offerDone.Set()
                Some(res)

    member self.resetToIdle () =
        Interlocked.Exchange(&state, 0) |> ignore

    member self.offer (e: MouseEvent): bool =
        // Write data before state transition to prevent consumer reading stale slot
        slot <- e
        Thread.MemoryBarrier()

        // Reset before CAS to prevent missing signal from poll thread
        offerDone.Reset()

        // Try WAITING -> OFFERED
        if Interlocked.CompareExchange(&state, 2, 1) <> 1 then
            false
        else
            event.Set()

            // Wait until waiter transitions away from OFFERED
            offerDone.Wait(150) |> ignore

            true


let private THREAD_PRIORITY = ThreadPriority.AboveNormal

//let private waiting = ref false
let private waitingEvent: MouseEvent ref = ref NonEvent

let private sync = new SynchronousQueue()

let private setFlagsOffer me =
    let we = Volatile.Read(waitingEvent)
    match me with
    | Move(_) ->
        Debug.WriteLine(sprintf "setFlagsOffer - setResent (Move): %s" we.Name)
        Ctx.LastFlags.SetResent we
        //Thread.Sleep(0)
    | LeftUp(_) | RightUp(_) ->
        Debug.WriteLine(sprintf "setFlagsOffer - setResent (Up): %s" we.Name)
        Ctx.LastFlags.SetResent we
    | LeftDown(_) | RightDown(_) ->
        Debug.WriteLine(sprintf "setFlagsOffer - setSuppressed: %s" we.Name)
        Ctx.LastFlags.SetSuppressed we
        Ctx.LastFlags.SetSuppressed me
        Ctx.setStartingScrollMode()
    | Cancel -> Debug.WriteLine("setFlagsOffer: cancel")
    | _ -> raise (InvalidOperationException())

let offer me: bool =
    if sync.offer(me) then
        setFlagsOffer me
        true 
    else
        false

let setOfferEW () =
    Ctx.setOfferEW offer

let private fromMove (down: MouseEvent) =
    //Ctx.LastFlags.SetResent down
    Debug.WriteLine(sprintf "wait Trigger (%s -->> Move): resend %s" down.Name down.Name)
    Windows.resendDown down

let private fromUp (down:MouseEvent) (up:MouseEvent) =
    //Ctx.LastFlags.SetResent down

    let resendC (mc: MouseClick) =
        Debug.WriteLine(sprintf "wait Trigger (%s -->> %s): resend %s" down.Name up.Name mc.Name)
        Windows.resendClick mc

    let resendUD () =
        let wn = down.Name
        let rn = up.Name
        Debug.WriteLine(sprintf "wait Trigger (%s -->> %s): resend %s, %s" wn rn wn rn)
        Windows.resendDown down
        Windows.resendUp up

    match down, up with
    | LeftDown(_), LeftUp(_)  ->
        if Mouse.samePoint down up then
            resendC(LeftClick(down.Info))
        else
            resendUD()
    | LeftDown(_), RightUp(_) -> resendUD()
    | RightDown(_), RightUp(_) ->
        if Mouse.samePoint down up then
            resendC(RightClick(down.Info))
        else
            resendUD()
    | RightDown(_), LeftUp(_) -> resendUD()
    | _ -> raise (InvalidOperationException())

let private fromDown (d1:MouseEvent) (d2:MouseEvent) =
    //Ctx.LastFlags.SetSuppressed d1
    //Ctx.LastFlags.SetSuppressed d2

    Debug.WriteLine(sprintf "wait Trigger (%s -->> %s): start scroll mode" d1.Name d2.Name)
    Ctx.startScrollMode d2.Info

let private dispatchEvent down res =
    match res with
    | Move(_) -> fromMove down
    | LeftUp(_) | RightUp(_) -> fromUp down res
    | LeftDown(_) | RightDown(_) -> fromDown down res
    | Cancel -> Debug.WriteLine("dispatchEvent: cancel")
    | _ -> raise (InvalidOperationException())

let private fromTimeout down =
    Ctx.LastFlags.SetResent down
    Debug.WriteLine(sprintf "wait Trigger (%s -->> Timeout): resend %s" down.Name down.Name)
    Windows.resendDown down

let private waiterQueue = new BlockingCollection<MouseEvent>(1024)

let private waiterThread = new Thread(fun () ->
    while true do
        try
            let down = waiterQueue.Take()

            match sync.poll(Ctx.getPollTimeout()) with
            | Some(res) -> dispatchEvent down res
            | None -> fromTimeout down
        with
        | :? ObjectDisposedException -> () // Queue disposed during shutdown
        | e -> Debug.WriteLine(sprintf "waiterThread error: %s" e.Message)
)
        
//let private waiterThread = new Thread(waiter)
waiterThread.IsBackground <- true
waiterThread.Priority <- THREAD_PRIORITY
waiterThread.Start()

let start (down: MouseEvent) =
    if not (down.IsDown) then
        raise (ArgumentException())

    Volatile.Write(waitingEvent, down)
    sync.setWaiting()
    if waiterQueue.TryAdd(down, 0) then
        true
    else
        sync.resetToIdle()
        false


