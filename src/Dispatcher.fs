/// Low-level mouse and keyboard hook dispatchers.
///
/// Routes Windows hook callbacks to the appropriate event handlers based on
/// the message type (mouse move, button press, key press, etc.).
///
/// The mouse dispatcher handles:
/// - WM_MOUSEMOVE → EventHandler.move
/// - WM_LBUTTONDOWN/UP, WM_RBUTTONDOWN/UP → EventHandler.left*/right*
/// - WM_MBUTTONDOWN/UP, WM_XBUTTONDOWN/UP → EventHandler.middle*/x*
/// - WM_MOUSEWHEEL, WM_MOUSEHWHEEL → pass through (or process IPC message)
///
/// The keyboard dispatcher handles:
/// - WM_KEYDOWN, WM_SYSKEYDOWN → KEventHandler.keyDown
/// - WM_KEYUP, WM_SYSKEYUP → KEventHandler.keyUp
///
/// Both dispatchers respect pass mode: when enabled, all events pass through
/// without processing (except for IPC messages).
module Dispatcher

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.Diagnostics
open System.Threading
open System.Windows.Forms
open WinAPI.Message

type HookInfo = WinAPI.MSLLHOOKSTRUCT
type KHookInfo = WinAPI.KBDLLHOOKSTRUCT

let private procCommand (info: HookInfo): bool =
    if (info.mouseData >>> 16) <> 1u then
        false
    else
        Debug.WriteLine("receive (mouseData >>> 16) = 1")
        let msg = int (info.dwExtraInfo.ToUInt32())
        W10Message.procMessage msg

let private mouseProc nCode wParam info: nativeint =
    //Debug.WriteLine("mouseProc")
    let callNextHook = (fun _ -> WinHook.callNextMouseHook nCode wParam info)
    EventHandler.setCallNextHook callNextHook
    if nCode < 0 then
        callNextHook()
    else
        if Ctx.isPassMode() then
            if wParam.ToInt32() = WM_MOUSEHWHEEL && procCommand info then
                IntPtr(1)
            else
                callNextHook()
        else
            match wParam.ToInt32() with
            | WM_MOUSEMOVE -> EventHandler.move info
            | WM_LBUTTONDOWN -> EventHandler.leftDown info
            | WM_LBUTTONUP -> EventHandler.leftUp info
            | WM_RBUTTONDOWN -> EventHandler.rightDown info
            | WM_RBUTTONUP -> EventHandler.rightUp info
            | WM_MBUTTONDOWN -> EventHandler.middleDown info
            | WM_MBUTTONUP -> EventHandler.middleUp info
            | WM_XBUTTONDOWN -> EventHandler.xDown info
            | WM_XBUTTONUP -> EventHandler.xUp info
            | WM_MOUSEWHEEL -> callNextHook()
            | WM_MOUSEHWHEEL ->
                if procCommand info then IntPtr(1) else callNextHook()
            | msg ->
                Debug.WriteLine(sprintf "Unknown mouse message: 0x%X" msg)
                callNextHook()

let private getMouseDispatcher () = new WinAPI.LowLevelMouseProc(mouseProc)

let setMouseDispatcher () =
    WinHook.setMouseDispatcher (getMouseDispatcher ())

let private keyboardProc nCode wParam (info:KHookInfo): nativeint =
    let callNextHook = (fun _ -> WinHook.callNextKeyboardHook nCode wParam info)
    KEventHandler.setCallNextHook callNextHook
    if nCode < 0 || Ctx.isPassMode() then
        callNextHook()
    else
        match wParam.ToInt32() with
        | WM_KEYDOWN | WM_SYSKEYDOWN -> KEventHandler.keyDown info
        | WM_KEYUP | WM_SYSKEYUP -> KEventHandler.keyUp info
        | _ -> callNextHook()

let private getKeyboardDispatcher () = new WinAPI.LowLevelKeyboardProc(keyboardProc)

let setKeyboardDispatcher () =
    WinHook.setKeyboardDispatcher (getKeyboardDispatcher ())

