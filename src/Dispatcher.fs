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
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.Threading
open System.Windows.Forms
open WinAPI.Message

type HookInfo = WinAPI.MSLLHOOKSTRUCT
type KHookInfo = WinAPI.KBDLLHOOKSTRUCT

// Module-level state for current hook parameters (avoids per-event closure allocation)
let mutable private curMouseNCode = 0
let mutable private curMouseWParam = IntPtr.Zero
let mutable private curMouseLParam = IntPtr.Zero

let private callNextMouse () =
    WinHook.callNextMouseHook curMouseNCode curMouseWParam curMouseLParam

let mutable private curKbNCode = 0
let mutable private curKbWParam = IntPtr.Zero
let mutable private curKbLParam = IntPtr.Zero

let private callNextKb () =
    WinHook.callNextKeyboardHook curKbNCode curKbWParam curKbLParam

let private mouseProc nCode wParam (lParam: nativeint): nativeint =
    // nCode < 0: lParam may be invalid; pass through without dereferencing
    if nCode < 0 then
        WinHook.callNextMouseHook nCode wParam lParam
    else
        Ctx.hookAlive()
        let info = Marshal.PtrToStructure<HookInfo>(lParam)
        // Save/restore statics for re-entrancy (SendInput can re-enter the hook)
        let prevNCode = curMouseNCode
        let prevWParam = curMouseWParam
        let prevLParam = curMouseLParam
        curMouseNCode <- nCode
        curMouseWParam <- wParam
        curMouseLParam <- lParam
        EventHandler.setCallNextHook callNextMouse
        try
            try
                if Ctx.isPassMode() then
                    callNextMouse()
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
                    | WM_MOUSEWHEEL -> callNextMouse()
                    | WM_MOUSEHWHEEL -> callNextMouse()
                    | msg ->
                        Debug.WriteLine(sprintf "Unknown mouse message: 0x%X" msg)
                        callNextMouse()
            with _ -> callNextMouse()
        finally
            curMouseNCode <- prevNCode
            curMouseWParam <- prevWParam
            curMouseLParam <- prevLParam

let private getMouseDispatcher () = new WinAPI.LowLevelMouseProc(mouseProc)

let setMouseDispatcher () =
    WinHook.setMouseDispatcher (getMouseDispatcher ())

let private keyboardProc nCode wParam (lParam: nativeint): nativeint =
    // nCode < 0: lParam may be invalid; pass through without dereferencing
    if nCode < 0 then
        WinHook.callNextKeyboardHook nCode wParam lParam
    else
        let info = Marshal.PtrToStructure<KHookInfo>(lParam)
        // Save/restore statics for re-entrancy (SendInput can re-enter the hook)
        let prevNCode = curKbNCode
        let prevWParam = curKbWParam
        let prevLParam = curKbLParam
        curKbNCode <- nCode
        curKbWParam <- wParam
        curKbLParam <- lParam
        KEventHandler.setCallNextHook callNextKb
        try
            try
                if Ctx.isPassMode() then
                    callNextKb()
                else
                    match wParam.ToInt32() with
                    | WM_KEYDOWN | WM_SYSKEYDOWN -> KEventHandler.keyDown info
                    | WM_KEYUP | WM_SYSKEYUP -> KEventHandler.keyUp info
                    | _ -> callNextKb()
            with _ -> callNextKb()
        finally
            curKbNCode <- prevNCode
            curKbWParam <- prevWParam
            curKbLParam <- prevLParam

let private getKeyboardDispatcher () = new WinAPI.LowLevelKeyboardProc(keyboardProc)

let setKeyboardDispatcher () =
    WinHook.setKeyboardDispatcher (getKeyboardDispatcher ())

