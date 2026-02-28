/// Global application context and state management.
///
/// This is the central state module that coordinates all other modules.
/// Contains all runtime state including:
///
/// - Scroll mode state: scrollMode, scrollStartPoint, scrollLockTime
/// - Trigger settings: firstTrigger, pollTimeout, dragThreshold
/// - Scroll settings: realWheelMode, cursorChange, reverseScroll, etc.
/// - Acceleration: accelTable, customAccelTable, multiplier presets
/// - VH Adjuster: vhAdjusterMode, firstPreferVertical, switchingThreshold
/// - Keyboard trigger: keyboardHook, targetVKCode
/// - UI: system tray menu, NotifyIcon
///
/// Key responsibilities:
/// - Loading/saving properties from/to file
/// - Coordinating state changes across modules via callbacks
/// - Managing the system tray context menu
/// - Providing thread-safe accessors for all settings (via Volatile)
///
/// The module uses callback injection (set* functions) to break circular
/// dependencies between modules while maintaining centralized state.
module Ctx

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Diagnostics
open System.Globalization
open System.Threading
open System.Windows.Forms
open System.Reflection
open System.IO
open System.Collections.Generic
open System.Security.Principal

open Mouse
open Keyboard

let private ICON_RUN_NAME = "icon-run.ico"
let private ICON_STOP_NAME = "icon-stop.ico"

let private selectedProperties: string ref = ref Properties.DEFAULT_DEF

let setSelectedProperties name =
    Volatile.Write(selectedProperties, name)

let getSelectedProperties () =
    Volatile.Read(selectedProperties)

let private firstTrigger: Trigger ref = ref LRTrigger
let private pollTimeout = ref 200
let private processPriority = ref ProcessPriority.AboveNormal
let private sendMiddleClick = ref false

let private keyboardHook = ref false
let private targetVKCode = ref (Keyboard.getVKCode(DataID.VK_NONCONVERT))

let private dragThreshold = ref 0

let private hookHealthCheck = ref 0
let private hookAliveFlag = ref 0

(* Filter Keys *)
let private fkEnabled = ref false
let private fkLock = ref false
let private fkAcceptanceDelay = ref 1000
let private fkRepeatDelay = ref 1000
let private fkRepeatRate = ref 500
let private fkBounceTime = ref 0
let private kbRepeatDelay = ref 1
let private kbRepeatSpeed = ref 31
let mutable private lastPtX = 0
let mutable private lastPtY = 0

let private healthTimer =
    let t = new System.Windows.Forms.Timer()
    t.Tick.Add(fun _ ->
        let pt = Cursor.Position
        let moved = (pt.X <> lastPtX || pt.Y <> lastPtY)
        lastPtX <- pt.X
        lastPtY <- pt.Y
        if moved && Interlocked.Exchange(hookAliveFlag, 0) = 0 then
            WinHook.unhookMouse()
            WinHook.setMouseHook() |> ignore
    )
    t

let updateHealthTimer () =
    healthTimer.Stop()
    let interval = Volatile.Read(hookHealthCheck)
    if interval > 0 then
        healthTimer.Interval <- interval * 1000
        healthTimer.Start()

let TRIGGER_NAMES = [|
    "LR (Left <<-->> Right)"; "Left (Left -->> Right)"
    "Right (Right -->> Left)"; "Middle"; "X1"; "X2"
    "LeftDrag"; "RightDrag"; "MiddleDrag"; "X1Drag"; "X2Drag"; "None"
|]

let private TRIGGER_IDS = [|
    DataID.LR; DataID.Left; DataID.Right; DataID.Middle
    DataID.X1; DataID.X2; DataID.LeftDrag; DataID.RightDrag
    DataID.MiddleDrag; DataID.X1Drag; DataID.X2Drag; DataID.None
|]

let VK_NAMES = [|
    "VK_TAB (Tab)"; "VK_PAUSE (Pause)"; "VK_CAPITAL (Caps Lock)"
    "VK_CONVERT (Henkan)"; "VK_NONCONVERT (Muhenkan)"
    "VK_PRIOR (Page Up)"; "VK_NEXT (Page Down)"
    "VK_END (End)"; "VK_HOME (Home)"
    "VK_SNAPSHOT (Print Screen)"; "VK_INSERT (Insert)"; "VK_DELETE (Delete)"
    "VK_LWIN (Left Windows)"; "VK_RWIN (Right Windows)"; "VK_APPS (Application)"
    "VK_NUMLOCK (Number Lock)"; "VK_SCROLL (Scroll Lock)"
    "VK_LSHIFT (Left Shift)"; "VK_RSHIFT (Right Shift)"
    "VK_LCONTROL (Left Ctrl)"; "VK_RCONTROL (Right Ctrl)"
    "VK_LMENU (Left Alt)"; "VK_RMENU (Right Alt)"
    "None"
|]

let mutable private showSettings: (unit -> unit) = (fun () -> ())
let setShowSettings (f: unit -> unit) = showSettings <- f

let getDragThreshold () =
    Volatile.Read(dragThreshold)

let getTargetVKCode () =
    Volatile.Read(targetVKCode)

let isTriggerKey (ke: KeyboardEvent) =
    ke.VKCode = getTargetVKCode()

let isSendMiddleClick () =
    Volatile.Read(sendMiddleClick)

let private notifyIcon = new System.Windows.Forms.NotifyIcon()

let mutable private passModeMenuItem: ToolStripMenuItem = null

let private getTrayText b =
    sprintf "%s - %s" AppDef.PROGRAM_NAME (if b then "Stopped" else "Runnable")

let private getIcon (name: string) =
    let stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
    new Drawing.Icon(stream)

let private iconRun = lazy (getIcon ICON_RUN_NAME)
let private iconStop = lazy (getIcon ICON_STOP_NAME)

let private getTrayIcon b =
    if b then iconStop.Value else iconRun.Value

let private changeNotifyIcon b =
    notifyIcon.Text <- (getTrayText b)
    notifyIcon.Icon <- (getTrayIcon b)

type private Pass() =
    [<VolatileField>] static let mutable mode = false

    static member Mode
        with get() = mode
        and set b =
            mode <- b
            changeNotifyIcon b
            passModeMenuItem.Checked <- b

    static member toggleMode () =
        Pass.Mode <- not mode

// MouseWorks by Kensington (DefaultAccelThreshold, M5, M6, M7, M8, M9)
// http://www.nanayojapan.co.jp/support/help/tmh00017.htm

[<AbstractClass>]
type AccelMultiplier(name: string, dArray: double[]) =
    member self.Name = name
    member self.DArray = dArray

type M5() = inherit AccelMultiplier(DataID.M5, [|1.0; 1.3; 1.7; 2.0; 2.4; 2.7; 3.1; 3.4; 3.8; 4.1; 4.5; 4.8|])
type M6() = inherit AccelMultiplier(DataID.M6, [|1.2; 1.6; 2.0; 2.4; 2.8; 3.3; 3.7; 4.1; 4.5; 4.9; 5.4; 5.8|])
type M7() = inherit AccelMultiplier(DataID.M7, [|1.4; 1.8; 2.3; 2.8; 3.3; 3.8; 4.3; 4.8; 5.3; 5.8; 6.3; 6.7|])
type M8() = inherit AccelMultiplier(DataID.M8, [|1.6; 2.1; 2.7; 3.2; 3.8; 4.4; 4.9; 5.5; 6.0; 6.6; 7.2; 7.7|])
type M9() = inherit AccelMultiplier(DataID.M9, [|1.8; 2.4; 3.0; 3.6; 4.3; 4.9; 5.5; 6.2; 6.8; 7.4; 8.1; 8.7|])

let getAccelMultiplierOfName name: AccelMultiplier =
    match name with
    | DataID.M5 -> (M5() :> AccelMultiplier)
    | DataID.M6 -> (M6() :> AccelMultiplier)
    | DataID.M7 -> (M7() :> AccelMultiplier)
    | DataID.M8 -> (M8() :> AccelMultiplier)
    | DataID.M9 -> (M9() :> AccelMultiplier)
    | e -> raise (ArgumentException(e))

let DefaultAccelThreshold = [|1; 2; 3; 5; 7; 10; 14; 20; 30; 43; 63; 91|]

type private Accel() =
    [<VolatileField>] static let mutable table = true
    [<VolatileField>] static let mutable threshold: int[] = DefaultAccelThreshold
    [<VolatileField>] static let mutable multiplier: AccelMultiplier = M5() :> AccelMultiplier

    [<VolatileField>] static let mutable customDisabled = true
    [<VolatileField>] static let mutable customTable = false
    [<VolatileField>] static let mutable customThreshold: int[] = null
    [<VolatileField>] static let mutable customMultiplier: double[] = null

    static member Table
        with get() = table
        and set b = table <- b
    static member Threshold
        with get() = threshold
    static member Multiplier
        with get() = multiplier
        and set m = multiplier <- m
    static member CustomDisabled
        with get() = customDisabled
        and set b = customDisabled <- b
    static member CustomTable
        with get() = customTable
        and set b = customTable <- b
    static member CustomThreshold
        with get() = customThreshold
        and set a = customThreshold <- a
    static member CustomMultiplier
        with get() = customMultiplier
        and set a = customMultiplier <- a

let isAccelTable () =
    Accel.Table

let getAccelThreshold () =
    if not Accel.CustomDisabled && Accel.CustomTable then
        Accel.CustomThreshold
    else
        Accel.Threshold

let getAccelMultiplier () =
    if not Accel.CustomDisabled && Accel.CustomTable then
        Accel.CustomMultiplier
    else
        Accel.Multiplier.DArray

let private getFirstTrigger () =
    Volatile.Read(firstTrigger)

let getProcessPriority () =
    Volatile.Read(processPriority)

let isTrigger (e: Trigger) =
    getFirstTrigger() = e

let isTriggerEvent (e: MouseEvent) =
    isTrigger(getTrigger e)

let isLRTrigger () =
    isTrigger LRTrigger

let isDragTriggerEvent = function
    | LeftEvent(_) -> isTrigger(LeftDragTrigger)
    | RightEvent(_) -> isTrigger(RightDragTrigger)
    | MiddleEvent(_) -> isTrigger(MiddleDragTrigger)
    | X1Event(_) -> isTrigger(X1DragTrigger)
    | X2Event(_) -> isTrigger(X2DragTrigger)
    | _ -> raise (ArgumentException())

let isSingleTrigger () =
    getFirstTrigger().IsSingle

let isDoubleTrigger () =
    getFirstTrigger().IsDouble

let isDragTrigger () =
    getFirstTrigger().IsDrag

type private Threshold() =
    [<VolatileField>] static let mutable vertical = 0
    [<VolatileField>] static let mutable horizontal = 75

    static member Vertical
        with get() = vertical
        and set n = vertical <- n
    static member Horizontal
        with get() = horizontal
        and set n = horizontal <- n

let getVerticalThreshold () = Threshold.Vertical
let getHorizontalThreshold () = Threshold.Horizontal

type private RealWheel() =
    [<VolatileField>] static let mutable mode = false
    [<VolatileField>] static let mutable wheelDelta = 120
    [<VolatileField>] static let mutable vWheelMove = 60
    [<VolatileField>] static let mutable hWheelMove = 60
    [<VolatileField>] static let mutable quickFirst = false
    [<VolatileField>] static let mutable quickTurn = false

    static member Mode
        with get() = mode
        and set b = mode <- b
    static member WheelDelta
        with get() = wheelDelta
        and set n = wheelDelta <- n
    static member VWheelMove
        with get() = vWheelMove
        and set n = vWheelMove <- n
    static member HWheelMove
        with get() = hWheelMove
        and set n = hWheelMove <- n
    static member QuickFirst
        with get() = quickFirst
        and set b = quickFirst <- b
    static member QuickTurn
        with get() = quickTurn
        and set b = quickTurn <- b

let isRealWheelMode () =
    RealWheel.Mode

let getWheelDelta () =
    RealWheel.WheelDelta

let getVWheelMove () =
    RealWheel.VWheelMove

let getHWheelMove () =
    RealWheel.HWheelMove

let isQuickFirst () =
    RealWheel.QuickFirst

let isQuickTurn () =
    RealWheel.QuickTurn

let mutable private initScroll: (unit -> unit) = (fun () -> ())

let setInitScroll (f: unit -> unit) =
    initScroll <- f

type private Scroll() =
    [<VolatileField>] static let mutable starting = false
    [<VolatileField>] static let mutable mode = false
    [<VolatileField>] static let mutable stime = 0u
    [<VolatileField>] static let mutable sx = 0
    [<VolatileField>] static let mutable sy = 0
    [<VolatileField>] static let mutable locktime = 200
    [<VolatileField>] static let mutable cursorChange = true
    [<VolatileField>] static let mutable reverse = false
    [<VolatileField>] static let mutable horizontal = true
    [<VolatileField>] static let mutable draggedLock = false
    [<VolatileField>] static let mutable swap = false
    [<VolatileField>] static let mutable releasedMode = false

    static let monitor = new Object()

    static let setStartPoint (x: int, y: int) =
        sx <- x
        sy <- y

    static member Start (info: HookInfo) = lock monitor (fun () ->
        stime <- info.time
        setStartPoint(info.pt.x, info.pt.y)
        initScroll()

        RawInput.register()

        if cursorChange && not (isDragTrigger()) then
            WinCursor.changeV()

        mode <- true
        starting <- false
    )

    static member Start (info: KHookInfo) = lock monitor (fun () ->
        stime <- info.time
        setStartPoint(Cursor.Position.X, Cursor.Position.Y)
        initScroll()

        RawInput.register()

        if cursorChange then
            WinCursor.changeV()

        mode <- true
        starting <- false
    )

    static member Exit () = lock monitor (fun () ->
        RawInput.unregister()

        mode <- false
        releasedMode <- false

        if cursorChange then
            WinCursor.restore() |> ignore
    )

    static member CheckExit (time: uint32) =
        let dt = time - stime
        Debug.WriteLine(sprintf "scroll time: %d ms" dt)
        dt > (uint32 locktime)

    static member IsMode with get() = mode
    static member StartTime with get() = stime
    static member StartPoint with get() = (sx, sy)
    static member LockTime
        with get() = locktime
        and set n = locktime <- n
    static member CursorChange
        with get() = cursorChange
        and set b = cursorChange <- b
    static member Reverse
        with get() = reverse
        and set b = reverse <- b
    static member Horizontal
        with get() = horizontal
        and set b = horizontal <- b
    static member DraggedLock
        with get() = draggedLock
        and set b = draggedLock <- b
    static member Swap
        with get() = swap
        and set b = swap <- b
    static member ReleasedMode
        with get() = releasedMode
        and set b = releasedMode <- b

    static member SetStarting () = lock monitor (fun () ->
        starting <- (not mode)
    )

    static member IsStarting with get() = starting

let isScrollMode () = Scroll.IsMode
let startScrollMode (info: HookInfo): unit = Scroll.Start info
let startScrollModeK (info: KHookInfo) = Scroll.Start info

let exitScrollMode (): unit = Scroll.Exit()
let checkExitScroll (time: uint32) = Scroll.CheckExit time
let getScrollStartPoint () = Scroll.StartPoint
let isCursorChange () = Scroll.CursorChange
let isReverseScroll () = Scroll.Reverse
let isHorizontalScroll () = Scroll.Horizontal
let isDraggedLock () = Scroll.DraggedLock
let isSwapScroll () = Scroll.Swap

let isReleasedScrollMode () = Scroll.ReleasedMode
let isPressedScrollMode () = Scroll.IsMode && not (Scroll.ReleasedMode)
let setReleasedScrollMode () = Scroll.ReleasedMode <- true

let setStartingScrollMode () = Scroll.SetStarting()
let isStartingScrollMode () = Scroll.IsStarting

type VHAdjusterMethod =
    | Fixed
    | Switching

    member self.Name =
        Mouse.getUnionCaseName(self)

type private VHAdjuster() =
    [<VolatileField>] static let mutable mode = false
    [<VolatileField>] static let mutable _method: VHAdjusterMethod = Switching
    [<VolatileField>] static let mutable firstPreferVertical = true
    [<VolatileField>] static let mutable firstMinThreshold = 5
    [<VolatileField>] static let mutable switchingThreshold = 50

    static member Mode
        with get() = mode
        and set b = mode <- b
        
    static member Method
        with get() = _method
        and set m = _method <- m
        
    static member FirstPreferVertical
        with get() = firstPreferVertical
        and set b = firstPreferVertical <- b
        
    static member FirstMinThreshold
        with get() = firstMinThreshold
        and set n = firstMinThreshold <- n
        
    static member SwitchingThreshold
        with get() = switchingThreshold
        and set n = switchingThreshold <- n    

let isVhAdjusterMode () =
    (isHorizontalScroll()) && VHAdjuster.Mode

let getVhAdjusterMethod () =
    VHAdjuster.Method

let isVhAdjusterSwitching () =
    getVhAdjusterMethod() = Switching

let isFirstPreferVertical () =
    VHAdjuster.FirstPreferVertical

let getFirstMinThreshold () =
    VHAdjuster.FirstMinThreshold

let getSwitchingThreshold () =
    VHAdjuster.SwitchingThreshold

type LastFlags() =
    // R = Resent
    [<VolatileField>] static let mutable ldR = false
    [<VolatileField>] static let mutable rdR = false

    // P = Passed
    [<VolatileField>] static let mutable ldP = false
    [<VolatileField>] static let mutable rdP = false

    // S = Suppressed
    [<VolatileField>] static let mutable ldS = false
    [<VolatileField>] static let mutable rdS = false
    [<VolatileField>] static let mutable sdS = false

    static let kdS = Array.create 256 false

    static let getAndReset (flag: bool byref) =
        let res = flag
        flag <- false
        res

    static member Init () =
        ldR <- false; rdR <- false
        ldP <- false; rdP <- false
        ldS <- false; rdS <- false; sdS <- false
        Array.fill kdS 0 (kdS.Length) false

    static member SetResent (down: MouseEvent): unit =
        match down with
        | LeftDown(_) -> ldR <- true
        | RightDown(_) -> rdR <- true
        | _ -> raise (ArgumentException())

    static member GetAndReset_ResentDown (up: MouseEvent) =
        match up with
        | LeftUp(_) -> getAndReset(&ldR)
        | RightUp(_) -> getAndReset(&rdR)
        | _ -> raise (ArgumentException())

    static member SetPassed (down: MouseEvent): unit =
        match down with
        | LeftDown(_) -> ldP <- true
        | RightDown(_) -> rdP <- true
        | _ -> raise (ArgumentException())

    static member GetAndReset_PassedDown (up: MouseEvent) =
        match up with
        | LeftUp(_) -> getAndReset(&ldP)
        | RightUp(_) -> getAndReset(&rdP)
        | _ -> raise (ArgumentException())

    static member SetSuppressed (down: MouseEvent): unit =
        match down with
        | LeftDown(_) -> ldS <- true 
        | RightDown(_) -> rdS <- true
        | MiddleDown(_) | X1Down(_) | X2Down(_) -> sdS <- true
        | _ -> raise (ArgumentException())

    static member SetSuppressed (down: KeyboardEvent) =
        match down with
        | KeyDown(_) -> kdS.[down.VKCode &&& 0xFF] <- true
        | _ -> raise (ArgumentException())

    static member GetAndReset_SuppressedDown (up: MouseEvent) =
        match up with
        | LeftUp(_) -> getAndReset(&ldS)
        | RightUp(_) -> getAndReset(&rdS)
        | MiddleUp(_) | X1Up(_) | X2Up(_) -> getAndReset(&sdS)
        | _ -> raise (ArgumentException())

    static member GetAndReset_SuppressedDown (up: KeyboardEvent) =
        match up with
        | KeyUp(_) -> getAndReset(&kdS.[up.VKCode &&& 0xFF])
        | _ -> raise (ArgumentException())

    static member ResetLR (down: MouseEvent) =
        match down with
        | LeftDown(_) -> ldR <- false; ldS <- false; ldP <- false
        | RightDown(_) -> rdR <- false; rdS <- false; rdP <- false
        | _ -> raise (ArgumentException())

let getPollTimeout () =
    Volatile.Read(pollTimeout)

let isPassMode () =
    Pass.Mode

let setPassMode b =
    Pass.Mode <- b

type HookInfo = WinAPI.MSLLHOOKSTRUCT
type KHookInfo = WinAPI.KBDLLHOOKSTRUCT

let private getFirstWord (s: string): string =
    s.Split([|' '|]).[0]

let getFirstTriggerIndex () =
    let name = getFirstTrigger().Name
    TRIGGER_IDS |> Array.findIndex (fun id -> Mouse.getTriggerOfStr id = Mouse.getTriggerOfStr name)

let getTriggerIdOfIndex (index: int) = TRIGGER_IDS.[index]

let getAccelPresetName () = Accel.Multiplier.Name
let isCustomAccelDisabled () = Accel.CustomDisabled
let getCustomAccelThreshold () = Accel.CustomThreshold
let getCustomAccelMultiplier () = Accel.CustomMultiplier
let getProcessPriorityName () = getProcessPriority().Name
let getTargetVKCodeName () = Keyboard.getName(getTargetVKCode())
let getVhAdjusterMethodName () = getVhAdjusterMethod().Name

let getTargetVKCodeIndex () =
    let name = getTargetVKCodeName()
    VK_NAMES |> Array.findIndex (fun s -> getFirstWord s = name)

let getVKIdOfIndex (index: int) = getFirstWord VK_NAMES.[index]

let setCustomAccelStrings (thrStr: string) (mulStr: string) : bool =
    try
        let thr = thrStr.Split(',') |> Array.map (fun s -> Int32.Parse(s.Trim(), CultureInfo.InvariantCulture))
        let mul = mulStr.Split(',') |> Array.map (fun s -> Double.Parse(s.Trim(), CultureInfo.InvariantCulture))
        if thr.Length > 0 && thr.Length = mul.Length && thr.Length <= 64 then
            Accel.CustomThreshold <- thr
            Accel.CustomMultiplier <- mul
            Accel.CustomDisabled <- false
            true
        else
            false
    with _ -> false

let getBooleanOfName (name: string): bool =
    match name with
    | DataID.realWheelMode -> RealWheel.Mode
    | DataID.cursorChange -> Scroll.CursorChange
    | DataID.horizontalScroll -> Scroll.Horizontal
    | DataID.reverseScroll -> Scroll.Reverse
    | DataID.quickFirst -> RealWheel.QuickFirst
    | DataID.quickTurn -> RealWheel.QuickTurn
    | DataID.accelTable -> Accel.Table
    | DataID.customAccelTable -> Accel.CustomTable
    | DataID.draggedLock -> Scroll.DraggedLock
    | DataID.swapScroll -> Scroll.Swap
    | DataID.sendMiddleClick -> Volatile.Read(sendMiddleClick)
    | DataID.keyboardHook -> Volatile.Read(keyboardHook)
    | DataID.vhAdjusterMode -> VHAdjuster.Mode
    | DataID.firstPreferVertical -> VHAdjuster.FirstPreferVertical
    | DataID.passMode -> Pass.Mode
    | DataID.filterKeys -> Volatile.Read(fkEnabled)
    | DataID.fkLock -> Volatile.Read(fkLock)
    | e -> raise (ArgumentException(e))

let setBooleanOfName (name:string) (b:bool) =
    Debug.WriteLine(sprintf "setBoolean: %s = %s" name (b.ToString()))
    match name with
    | DataID.realWheelMode -> RealWheel.Mode <- b
    | DataID.cursorChange -> Scroll.CursorChange <- b
    | DataID.horizontalScroll -> Scroll.Horizontal <- b
    | DataID.reverseScroll -> Scroll.Reverse <- b
    | DataID.quickFirst -> RealWheel.QuickFirst <- b
    | DataID.quickTurn -> RealWheel.QuickTurn <- b
    | DataID.accelTable -> Accel.Table <- b
    | DataID.customAccelTable -> Accel.CustomTable <- b
    | DataID.draggedLock -> Scroll.DraggedLock <- b
    | DataID.swapScroll -> Scroll.Swap <- b
    | DataID.sendMiddleClick -> Volatile.Write(sendMiddleClick, b)
    | DataID.keyboardHook -> Volatile.Write(keyboardHook, b)
    | DataID.vhAdjusterMode -> VHAdjuster.Mode <- b
    | DataID.firstPreferVertical -> VHAdjuster.FirstPreferVertical <- b
    | DataID.passMode -> Pass.Mode <- b
    | DataID.filterKeys -> Volatile.Write(fkEnabled, b)
    | DataID.fkLock -> Volatile.Write(fkLock, b)
    | e -> raise (ArgumentException(e))

let mutable private changeTrigger: (unit -> unit) = (fun () -> ())

let setChangeTrigger (f: unit -> unit) =
    changeTrigger <- f

let setTrigger (text: string) =
    let res = Mouse.getTriggerOfStr text
    Debug.WriteLine("setTrigger: " + res.Name)
    Volatile.Write(firstTrigger, res)
    changeTrigger()


let setAccelMultiplier name =
    Debug.WriteLine("setAccelMultiplier " + name)
    Accel.Multiplier <- getAccelMultiplierOfName name


let setPriority name =
    let p = ProcessPriority.getPriority name
    Debug.WriteLine("setPriority: " + p.Name)
    Volatile.Write(processPriority, p)
    ProcessPriority.setPriority p


let getNumberOfName (name: string): int =
    match name with
    | DataID.pollTimeout -> Volatile.Read(pollTimeout)
    | DataID.scrollLocktime -> Scroll.LockTime
    | DataID.verticalThreshold -> Threshold.Vertical
    | DataID.horizontalThreshold -> Threshold.Horizontal
    | DataID.wheelDelta -> RealWheel.WheelDelta
    | DataID.vWheelMove -> RealWheel.VWheelMove
    | DataID.hWheelMove -> RealWheel.HWheelMove
    | DataID.firstMinThreshold -> VHAdjuster.FirstMinThreshold
    | DataID.switchingThreshold -> VHAdjuster.SwitchingThreshold
    | DataID.dragThreshold -> Volatile.Read(dragThreshold)
    | DataID.hookHealthCheck -> Volatile.Read(hookHealthCheck)
    | DataID.fkAcceptanceDelay -> Volatile.Read(fkAcceptanceDelay)
    | DataID.fkRepeatDelay -> Volatile.Read(fkRepeatDelay)
    | DataID.fkRepeatRate -> Volatile.Read(fkRepeatRate)
    | DataID.fkBounceTime -> Volatile.Read(fkBounceTime)
    | DataID.kbRepeatDelay -> Volatile.Read(kbRepeatDelay)
    | DataID.kbRepeatSpeed -> Volatile.Read(kbRepeatSpeed)
    | e -> raise (ArgumentException(e))

let setNumberOfName (name: string) (n: int): unit =
    Debug.WriteLine(sprintf "setNumber: %s = %d" name n)
    match name with
    | DataID.pollTimeout -> Volatile.Write(pollTimeout, n)
    | DataID.scrollLocktime -> Scroll.LockTime <- n
    | DataID.verticalThreshold -> Threshold.Vertical <- n
    | DataID.horizontalThreshold -> Threshold.Horizontal <- n
    | DataID.wheelDelta -> RealWheel.WheelDelta <- n
    | DataID.vWheelMove -> RealWheel.VWheelMove <- n
    | DataID.hWheelMove -> RealWheel.HWheelMove <- n
    | DataID.firstMinThreshold -> VHAdjuster.FirstMinThreshold <- n
    | DataID.switchingThreshold -> VHAdjuster.SwitchingThreshold <- n
    | DataID.dragThreshold -> Volatile.Write(dragThreshold, n)
    | DataID.hookHealthCheck ->
        Volatile.Write(hookHealthCheck, n)
        updateHealthTimer()
    | DataID.fkAcceptanceDelay -> Volatile.Write(fkAcceptanceDelay, n)
    | DataID.fkRepeatDelay -> Volatile.Write(fkRepeatDelay, n)
    | DataID.fkRepeatRate -> Volatile.Write(fkRepeatRate, n)
    | DataID.fkBounceTime -> Volatile.Write(fkBounceTime, n)
    | DataID.kbRepeatDelay -> Volatile.Write(kbRepeatDelay, n)
    | DataID.kbRepeatSpeed -> Volatile.Write(kbRepeatSpeed, n)
    | e -> raise (ArgumentException(e))


let getVhAdjusterMethodOfName name =
    match name with
    | DataID.Fixed -> Fixed
    | DataID.Switching -> Switching
    | _ -> raise (ArgumentException())

let setVhAdjusterMethod name =
    Debug.WriteLine("setVhAdjusterMethod: " + name)
    VHAdjuster.Method <- (getVhAdjusterMethodOfName name)

let setTargetVKCode name =
    Debug.WriteLine("setTargetVKCode: " + name)
    Volatile.Write(targetVKCode, Keyboard.getVKCode name)

let private setDefaultPriority () =
    Debug.WriteLine("setDefaultPriority")
    setPriority (getProcessPriority().Name)

let private setDefaultTrigger () =
    setTrigger(getFirstTrigger().Name)

let private NumberNames: string[] =
    [|DataID.pollTimeout; DataID.scrollLocktime;
      DataID.verticalThreshold; DataID.horizontalThreshold;
      DataID.wheelDelta; DataID.vWheelMove; DataID.hWheelMove;
      DataID.firstMinThreshold; DataID.switchingThreshold;
      DataID.dragThreshold; DataID.hookHealthCheck;
      DataID.fkAcceptanceDelay; DataID.fkRepeatDelay;
      DataID.fkRepeatRate; DataID.fkBounceTime;
      DataID.kbRepeatDelay; DataID.kbRepeatSpeed|]

let private BooleanNames: string[] =
    [|DataID.realWheelMode; DataID.cursorChange;
     DataID.horizontalScroll; DataID.reverseScroll;
     DataID.quickFirst; DataID.quickTurn;
     DataID.accelTable; DataID.customAccelTable;
     DataID.draggedLock; DataID.swapScroll;
     DataID.sendMiddleClick; DataID.keyboardHook;
     DataID.vhAdjusterMode; DataID.firstPreferVertical;
     DataID.filterKeys; DataID.fkLock|]

let private prop = Properties.Properties()

let private setStringOfProperty name setFunc =
    try
        setFunc(prop.GetString(name))
    with
        | :? KeyNotFoundException as e -> Debug.WriteLine("Not found: " + e.Message)
        | :? ArgumentException as e -> Debug.WriteLine("Match error: " + e.Message)

let private setTriggerOfProperty (): unit =
    setStringOfProperty DataID.firstTrigger setTrigger

let private setAccelOfProperty (): unit =
    setStringOfProperty DataID.accelMultiplier setAccelMultiplier

let private setCustomAccelOfProperty (): unit =
    try
        let cat = prop.GetIntArray(DataID.customAccelThreshold)
        let cam = prop.GetDoubleArray(DataID.customAccelMultiplier)

        if cat.Length <> 0 && cat.Length = cam.Length then
            Debug.WriteLine(sprintf "customAccelThreshold: %A" cat)
            Debug.WriteLine(sprintf "customAccelMultiplier: %A" cam)

            Accel.CustomThreshold <- cat
            Accel.CustomMultiplier <- cam
            Accel.CustomDisabled <- false
    with
        | :? KeyNotFoundException as e -> Debug.WriteLine("Not found: " + e.Message)
        | :? FormatException as e -> Debug.WriteLine("Parse error: " + e.Message)

let private setPriorityOfProperty (): unit =
    try
        setPriority (prop.GetString DataID.processPriority)
    with
        | :? KeyNotFoundException as e ->
            Debug.WriteLine("Not found " + e.Message)
            setDefaultPriority()
        | :? ArgumentException as e ->
            Debug.WriteLine("Match error: " + e.Message)
            setDefaultPriority()

let private setVKCodeOfProperty (): unit =
    setStringOfProperty DataID.targetVKCode setTargetVKCode

let private setVhAdjusterMethodOfProperty (): unit =
    setStringOfProperty DataID.vhAdjusterMethod setVhAdjusterMethod

let private setBooleanOfProperty (name: string): unit =
    try
        setBooleanOfName name (prop.GetBool name)
    with
        | :? KeyNotFoundException -> Debug.WriteLine("Not found: " + name)
        | :? FormatException -> Debug.WriteLine("Parse error: " + name)
        | :? ArgumentException -> Debug.WriteLine("Match error: " + name)

let private setNumberOfProperty (name:string) (low:int) (up:int) =
    try
        let n = prop.GetInt name
        Debug.WriteLine(sprintf "setNumberOfProperty: %s: %d" name n)
        if n < low || n > up then
            Debug.WriteLine("Number out of bounds: " + name)
        else
            setNumberOfName name n
    with
        | :? KeyNotFoundException ->
            Debug.WriteLine("Not fund: " + name)
            if name = DataID.dragThreshold then
                setNumberOfName name 0
        | :? FormatException -> Debug.WriteLine("Parse error: " + name)
        | :? ArgumentException -> Debug.WriteLine("Match error: " + name)

let private getSelectedPropertiesPath () =
    Properties.getPath (getSelectedProperties())

let mutable private loaded = false

let loadPropertiesFileOnly (): unit =
    try
        prop.Load(getSelectedPropertiesPath(), true)
    with
        | _ -> ()

let private setDefaults () =
    setTrigger DataID.LR
    setAccelMultiplier DataID.M5
    setPriority DataID.AboveNormal
    setTargetVKCode DataID.VK_NONCONVERT
    setVhAdjusterMethod DataID.Switching

    BooleanNames |> Array.iter (fun n -> setBooleanOfName n false)
    setBooleanOfName DataID.cursorChange true
    setBooleanOfName DataID.horizontalScroll true
    setBooleanOfName DataID.accelTable true
    setBooleanOfName DataID.firstPreferVertical true

    setNumberOfName DataID.pollTimeout 200
    setNumberOfName DataID.scrollLocktime 200
    setNumberOfName DataID.verticalThreshold 0
    setNumberOfName DataID.horizontalThreshold 75
    setNumberOfName DataID.wheelDelta 120
    setNumberOfName DataID.vWheelMove 60
    setNumberOfName DataID.hWheelMove 60
    setNumberOfName DataID.firstMinThreshold 5
    setNumberOfName DataID.switchingThreshold 50
    setNumberOfName DataID.dragThreshold 0
    setNumberOfName DataID.hookHealthCheck 0
    setNumberOfName DataID.fkAcceptanceDelay 1000
    setNumberOfName DataID.fkRepeatDelay 1000
    setNumberOfName DataID.fkRepeatRate 500
    setNumberOfName DataID.fkBounceTime 0
    setNumberOfName DataID.kbRepeatDelay 1
    setNumberOfName DataID.kbRepeatSpeed 31

    Accel.CustomDisabled <- true
    Accel.CustomThreshold <- null
    Accel.CustomMultiplier <- null

let loadProperties (update:bool): unit =
    loaded <- true
    if update then setDefaults()
    try
        prop.Load(getSelectedPropertiesPath(), update)
        Debug.WriteLine("Start load")

        setTriggerOfProperty()
        setAccelOfProperty()
        setCustomAccelOfProperty()
        setPriorityOfProperty()
        setVKCodeOfProperty()
        setVhAdjusterMethodOfProperty()

        BooleanNames |> Array.iter (fun n -> setBooleanOfProperty n)
        WinHook.setOrUnsetKeyboardHook (Volatile.Read(keyboardHook))

        Debug.WriteLine("setNumberOfProperties")
        let setNum = setNumberOfProperty
        setNum DataID.pollTimeout 50 500
        setNum DataID.scrollLocktime 150 500
        setNum DataID.verticalThreshold 0 500
        setNum DataID.horizontalThreshold 0 500

        setNum DataID.wheelDelta 10 500
        setNum DataID.vWheelMove 10 500
        setNum DataID.hWheelMove 10 500

        setNum DataID.firstMinThreshold 1 10
        setNum DataID.switchingThreshold 10 500
        setNum DataID.dragThreshold 0 500
        setNum DataID.hookHealthCheck 0 300

        setNum DataID.fkAcceptanceDelay 0 10000
        setNum DataID.fkRepeatDelay 0 10000
        setNum DataID.fkRepeatRate 0 10000
        setNum DataID.fkBounceTime 0 10000
        setNum DataID.kbRepeatDelay 0 3
        setNum DataID.kbRepeatSpeed 0 31
    with
        | :? FileNotFoundException ->
            Debug.WriteLine("Properties file not found")
            setDefaultPriority()
            setDefaultTrigger()
        | e -> Debug.WriteLine("load: " + (e.ToString()))

let private isChangedProperties () =
    try
        prop.Load(getSelectedPropertiesPath(), true)

        let isChangedBoolean () =
            BooleanNames |>
            Array.map (fun n -> (prop.GetBool n) <> getBooleanOfName n) |>
            Array.contains true
        let isChangedNumber () =
            NumberNames |>
            Array.map (fun n -> (prop.GetInt n) <> getNumberOfName n) |>
            Array.contains true

        let check = fun n v -> prop.GetString(n) <> v

        check DataID.firstTrigger (getFirstTrigger().Name) ||
        check DataID.accelMultiplier (Accel.Multiplier.Name) ||
        check DataID.processPriority (getProcessPriority().Name) ||
        check DataID.targetVKCode (Keyboard.getName(getTargetVKCode())) ||
        check DataID.vhAdjusterMethod (getVhAdjusterMethod().Name) ||
        isChangedBoolean() || isChangedNumber()
    with
        | :? FileNotFoundException -> Debug.WriteLine("First write properties"); true
        | :? KeyNotFoundException as e -> Debug.WriteLine("Not found: " + e.Message); true
        | :? TypeInitializationException as e -> Debug.WriteLine("TypeInit error (shutdown): " + e.Message); false
        | :? Runtime.InteropServices.SEHException as e -> Debug.WriteLine("SEH error (shutdown): " + e.Message); false
        | e -> Debug.WriteLine("isChanged: " + (e.ToString())); true

let storeProperties () =
    try
        if not loaded || not (isChangedProperties()) then
            Debug.WriteLine("Not changed properties")
        else
            let set = fun key value -> prop.[key] <- value

            set DataID.firstTrigger (getFirstTrigger().Name)
            set DataID.accelMultiplier (Accel.Multiplier.Name)
            set DataID.processPriority (getProcessPriority().Name)
            set DataID.targetVKCode (Keyboard.getName (getTargetVKCode()))
            set DataID.vhAdjusterMethod (getVhAdjusterMethod().Name)

            BooleanNames |> Array.iter (fun n -> prop.SetBool(n, (getBooleanOfName n)))
            NumberNames |> Array.iter (fun n -> prop.SetInt(n, (getNumberOfName n)))

            prop.Store(getSelectedPropertiesPath())
    with
        | e -> Debug.WriteLine("store: " + (e.ToString()))

let reloadProperties () =
    prop.Clear()
    loadProperties true
    updateHealthTimer()

let private DEFAULT_DEF = Properties.DEFAULT_DEF

let isValidPropertiesName name =
    (name <> DEFAULT_DEF) &&
    not (name.StartsWith("--")) &&
    not (name.Contains("\\")) &&
    not (name.Contains("/")) &&
    not (name.Contains(".."))

let setProperties name =
    if getSelectedProperties() <> name then
        Debug.WriteLine("setProperties: " + name)

        setSelectedProperties name
        loadProperties true

let exitAction () =
    notifyIcon.Visible <- false
    notifyIcon.Dispose()
    Application.Exit()

let private createContextMenuStrip (): ContextMenuStrip =
    let menu = new ContextMenuStrip()

    let isAdmin =
        let principal = new WindowsPrincipal(WindowsIdentity.GetCurrent())
        principal.IsInRole(WindowsBuiltInRole.Administrator)
    let statusItem = new ToolStripMenuItem(if isAdmin then "Running as Admin" else "Running as User")
    statusItem.Enabled <- false
    menu.Items.Add(statusItem) |> ignore
    menu.Items.Add(new ToolStripSeparator()) |> ignore

    let passItem = new ToolStripMenuItem("Pass Mode")
    passItem.CheckOnClick <- true
    passItem.Click.Add(fun _ ->
        setBooleanOfName DataID.passMode passItem.Checked
    )
    passModeMenuItem <- passItem
    menu.Items.Add(passItem) |> ignore

    let settingsItem = new ToolStripMenuItem("Settings...")
    settingsItem.Click.Add(fun _ -> showSettings())
    menu.Items.Add(settingsItem) |> ignore

    menu.Items.Add(new ToolStripSeparator()) |> ignore

    let exitItem = new ToolStripMenuItem("Exit")
    exitItem.Click.Add(fun _ -> exitAction())
    menu.Items.Add(exitItem) |> ignore

    menu

/// Hidden window that listens for Explorer's TaskbarCreated message
/// to re-add the tray icon after explorer.exe restarts.
type private TaskbarCreatedWindow() =
    inherit NativeWindow()
    static let wmTaskbarCreated = WinAPI.RegisterWindowMessageW("TaskbarCreated")
    do
        let cp = CreateParams()
        cp.Parent <- IntPtr.Zero
        base.CreateHandle(cp)
    override self.WndProc(m:Message byref): unit =
        if wmTaskbarCreated <> 0u && m.Msg = int wmTaskbarCreated then
            notifyIcon.Visible <- true
        base.WndProc(&m)

let private taskbarWatcher = lazy (TaskbarCreatedWindow())

let setSystemTray (): unit =
    let ni = notifyIcon
    ni.Icon <- getTrayIcon false
    ni.Text <- getTrayText false
    ni.Visible <- true
    ni.ContextMenuStrip <- createContextMenuStrip()
    ni.DoubleClick.Add (fun _ -> Pass.toggleMode())
    taskbarWatcher.Force() |> ignore
    
let mutable private initStateMEH: unit -> unit = (fun () -> ())
let mutable private initStateKEH: unit -> unit = (fun () -> ())
let mutable private offerEW: MouseEvent -> bool = (fun me -> false)

let setInitStateMEH f = initStateMEH <- f
let setInitStateKEH f = initStateKEH <- f
let setOfferEW f = offerEW <- f

let initState () =
    initStateMEH()
    initStateKEH()
    LastFlags.Init()
    exitScrollMode()
    offerEW(Cancel) |> ignore

let private fkDeleteLastRegistryValues () =
    try
        use key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\Keyboard Response", true)
        if key <> null then
            key.DeleteValue("Last BounceKey Setting", false)
            key.DeleteValue("Last Valid Delay", false)
            key.DeleteValue("Last Valid Repeat", false)
            key.DeleteValue("Last Valid Wait", false)
    with _ -> ()

let applyKeyboardRepeatToSystem (kbDelay: int) (kbSpeed: int) =
    WinAPI.SystemParametersInfo(WinAPI.SPI_SETKEYBOARDDELAY, uint32 kbDelay, nativeint 0, 0x0001u ||| 0x0002u) |> ignore
    WinAPI.SystemParametersInfo(WinAPI.SPI_SETKEYBOARDSPEED, uint32 kbSpeed, nativeint 0, 0x0001u ||| 0x0002u) |> ignore

let applyFilterKeysToSystem (enabled: bool) (accept: int) (delay: int) (repeat: int) (bounce: int) =
    let size = uint32 (Runtime.InteropServices.Marshal.SizeOf(typeof<WinAPI.FILTERKEYS>))
    let flags = if enabled then 59u else 126u
    let w = if enabled then uint32 accept else 1000u
    let d = if enabled then uint32 delay else 1000u
    let rp = if enabled then uint32 repeat else 500u
    let b = if enabled then uint32 bounce else 0u
    let ptr = Runtime.InteropServices.Marshal.AllocHGlobal(int size)
    try
        Runtime.InteropServices.Marshal.WriteInt32(ptr, 0, int size)
        Runtime.InteropServices.Marshal.WriteInt32(ptr, 4, int flags)
        Runtime.InteropServices.Marshal.WriteInt32(ptr, 8, int w)
        Runtime.InteropServices.Marshal.WriteInt32(ptr, 12, int d)
        Runtime.InteropServices.Marshal.WriteInt32(ptr, 16, int rp)
        Runtime.InteropServices.Marshal.WriteInt32(ptr, 20, int b)
        WinAPI.SystemParametersInfo(WinAPI.SPI_SETFILTERKEYS, size, ptr, 0x0001u ||| 0x0002u) |> ignore
    finally
        Runtime.InteropServices.Marshal.FreeHGlobal(ptr)
    if not enabled then
        fkDeleteLastRegistryValues()

let private readSystemFilterKeysRaw () =
    let size = uint32 (Runtime.InteropServices.Marshal.SizeOf(typeof<WinAPI.FILTERKEYS>))
    let ptr = Runtime.InteropServices.Marshal.AllocHGlobal(int size)
    try
        Runtime.InteropServices.Marshal.WriteInt32(ptr, int size)
        WinAPI.SystemParametersInfo(WinAPI.SPI_GETFILTERKEYS, size, ptr, 0u) |> ignore
        let flags = uint32 (Runtime.InteropServices.Marshal.ReadInt32(ptr + nativeint 4))
        let w = Runtime.InteropServices.Marshal.ReadInt32(ptr + nativeint 8)
        let d = Runtime.InteropServices.Marshal.ReadInt32(ptr + nativeint 12)
        let r = Runtime.InteropServices.Marshal.ReadInt32(ptr + nativeint 16)
        let b = Runtime.InteropServices.Marshal.ReadInt32(ptr + nativeint 20)
        ((flags &&& WinAPI.FKF_FILTERKEYSON) <> 0u, w, d, r, b)
    finally
        Runtime.InteropServices.Marshal.FreeHGlobal(ptr)

let applyFilterKeys () =
    let lock = Volatile.Read(fkLock)

    (* Keyboard repeat — only enforce if lock is on *)
    if lock then
        let cfgKbDelay = Volatile.Read(kbRepeatDelay)
        let cfgKbSpeed = Volatile.Read(kbRepeatSpeed)
        let ptrDelay = Runtime.InteropServices.Marshal.AllocHGlobal(4)
        let ptrSpeed = Runtime.InteropServices.Marshal.AllocHGlobal(4)
        try
            WinAPI.SystemParametersInfo(WinAPI.SPI_GETKEYBOARDDELAY, 0u, ptrDelay, 0u) |> ignore
            WinAPI.SystemParametersInfo(WinAPI.SPI_GETKEYBOARDSPEED, 0u, ptrSpeed, 0u) |> ignore
            let sysDelay = Runtime.InteropServices.Marshal.ReadInt32(ptrDelay)
            let sysSpeed = Runtime.InteropServices.Marshal.ReadInt32(ptrSpeed)
            if sysDelay <> cfgKbDelay || sysSpeed <> cfgKbSpeed then
                applyKeyboardRepeatToSystem cfgKbDelay cfgKbSpeed
        finally
            Runtime.InteropServices.Marshal.FreeHGlobal(ptrDelay)
            Runtime.InteropServices.Marshal.FreeHGlobal(ptrSpeed)

    (* Filter Keys *)
    let (sysEnabled, sysW, sysD, sysR, sysB) = readSystemFilterKeysRaw()
    let cfgEnabled = Volatile.Read(fkEnabled)

    if sysEnabled then
        if lock then
            // System enabled, lock on → enforce config values
            let cfgAccept = Volatile.Read(fkAcceptanceDelay)
            let cfgDelay = Volatile.Read(fkRepeatDelay)
            let cfgRepeat = Volatile.Read(fkRepeatRate)
            let cfgBounce = Volatile.Read(fkBounceTime)
            if sysW <> cfgAccept || sysD <> cfgDelay || sysR <> cfgRepeat || sysB <> cfgBounce then
                applyFilterKeysToSystem true cfgAccept cfgDelay cfgRepeat cfgBounce
        else
            // System enabled, lock off → sync system values to config
            Volatile.Write(fkEnabled, true)
            Volatile.Write(fkAcceptanceDelay, sysW)
            Volatile.Write(fkRepeatDelay, sysD)
            Volatile.Write(fkRepeatRate, sysR)
            Volatile.Write(fkBounceTime, sysB)
    elif cfgEnabled then
        if lock then
            // System disabled, config enabled, lock on → enable with config values
            let cfgAccept = Volatile.Read(fkAcceptanceDelay)
            let cfgDelay = Volatile.Read(fkRepeatDelay)
            let cfgRepeat = Volatile.Read(fkRepeatRate)
            let cfgBounce = Volatile.Read(fkBounceTime)
            applyFilterKeysToSystem true cfgAccept cfgDelay cfgRepeat cfgBounce
        else
            // System disabled, config enabled, lock off → enable with registry values
            applyFilterKeysToSystem true sysW sysD sysR sysB
            Volatile.Write(fkAcceptanceDelay, sysW)
            Volatile.Write(fkRepeatDelay, sysD)
            Volatile.Write(fkRepeatRate, sysR)
            Volatile.Write(fkBounceTime, sysB)

let hookAlive () =
    Interlocked.Exchange(hookAliveFlag, 1) |> ignore

let startHealthTimer () =
    updateHealthTimer()
