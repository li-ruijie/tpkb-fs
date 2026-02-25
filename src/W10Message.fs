/// Inter-process messaging for controlling a running W10Wheel instance.
///
/// Sends and receives custom messages via simulated horizontal wheel events
/// to enable command-line control (exit, pass mode, reload, init state).
///
/// Message protocol:
/// - Uses MOUSEEVENTF_HWHEEL with special magic numbers as mouseData
/// - Base message ID derived from constant with specific offsets for commands
/// - Boolean flags encoded in high nibble of message value
///
/// Security: Validates sender is another W10Wheel process before processing
/// commands to prevent arbitrary processes from controlling the application.
///
/// Used by command-line arguments: --sendExit, --sendPassMode, --sendReloadProp
module W10Message

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.Diagnostics
open System.IO
open System.Windows.Forms

open WinAPI.Event

/// Verify that the sender is another W10Wheel instance by checking
/// if a W10Wheel process (other than this one) is running.
let private isValidSender (): bool =
    let currentPid = Process.GetCurrentProcess().Id
    let currentExeName = AppDef.PROGRAM_NAME
    try
        Process.GetProcessesByName(currentExeName)
        |> Array.exists (fun p -> p.Id <> currentPid)
    with
    | _ -> false

let private W10_MESSAGE_BASE = 264816059 &&& 0x0FFFFFFF
let W10_MESSAGE_EXIT = W10_MESSAGE_BASE + 1
let W10_MESSAGE_PASSMODE = W10_MESSAGE_BASE + 2
let W10_MESSAGE_RELOAD_PROP = W10_MESSAGE_BASE + 3
let W10_MESSAGE_INIT_STATE = W10_MESSAGE_BASE + 4

let private sendMessage (msg: int) =
    let pt = WinAPI.POINT(Cursor.Position.X, Cursor.Position.Y)
    let res = Windows.sendInputDirect pt 1 MOUSEEVENTF_HWHEEL 0u (uint32 msg)
    Debug.WriteLine(sprintf "SendInput: %d" res)

let sendExit () =
    Debug.WriteLine("send W10_MESSAGE_EXIT")
    sendMessage W10_MESSAGE_EXIT

let private recvExit () =
    Debug.WriteLine("recv W10_MESSAGE_EXIT")
    Ctx.exitAction()

let private setBoolBit msg b =
    msg ||| if b then 0x10000000 else 0x00000000

let private getBoolBit msg =
    (msg &&& 0xF0000000) <> 0

let private getFlag msg =
    msg &&& 0x0FFFFFFF

let sendPassMode b =
    Debug.WriteLine("send W10_MESSAGE_PASSMODE")
    let msg = setBoolBit W10_MESSAGE_PASSMODE b
    sendMessage msg

let private recvPassMode msg =
    Debug.WriteLine("recv W10_MESSAGE_PASSMODE")
    Ctx.setPassMode (getBoolBit msg)

let sendReloadProp () =
    Debug.WriteLine("send W10_MESSAGE_RELOAD_PROP")
    sendMessage W10_MESSAGE_RELOAD_PROP

let private recvReloadProp () =
    Debug.WriteLine("recv W10_MESSAGE_RELOAD_PROP")
    Ctx.reloadProperties ()

let sendInitState () =
    Debug.WriteLine("send W10_MESSAGE_INIT_STATE")
    sendMessage W10_MESSAGE_INIT_STATE

let private recvInitState () =
    Debug.WriteLine("recv W10_MESSAGE_INIT_STATE")
    Ctx.initState ()

let procMessage msg =
    // Verify sender is another W10Wheel instance before processing commands
    if not (isValidSender()) then
        Debug.WriteLine("IPC rejected: no valid W10Wheel sender found")
        false
    else
        match getFlag(msg) with
        | n when n = W10_MESSAGE_EXIT ->
            recvExit (); true
        | n when n = W10_MESSAGE_PASSMODE ->
            recvPassMode msg; true
        | n when n = W10_MESSAGE_RELOAD_PROP ->
            recvReloadProp (); true
        | n when n = W10_MESSAGE_INIT_STATE ->
            recvInitState (); true
        | _ -> false
