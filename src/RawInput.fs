/// Windows Raw Input API integration for precise mouse movement tracking.
///
/// Registers for raw mouse input to get relative mouse deltas directly from
/// the hardware, bypassing mouse acceleration and providing accurate scroll control.
///
/// Raw Input is used during scroll mode to get precise movement deltas that
/// aren't affected by Windows mouse acceleration settings. This provides
/// consistent scroll behavior regardless of system configuration.
///
/// Implementation:
/// - Creates a message-only window (HWND_MESSAGE) to receive WM_INPUT messages
/// - Registers for mouse raw input using RegisterRawInputDevices
/// - Processes raw input data to extract relative movement (lLastX, lLastY)
/// - Forwards deltas to the scroll wheel simulation via sendWheelRaw callback
module RawInput

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.Windows.Forms
open System.Threading
open System.ComponentModel

open WinAPI.RawInput
open Dialog

/// Callback to send raw wheel movement to Windows.fs for scroll simulation.
/// Parameters are relative X and Y movement deltas.
let mutable private sendWheelRaw: int -> int -> unit = (fun x y -> ())

/// Sets the callback function for processing raw mouse movement.
let setSendWheelRaw f =
    sendWheelRaw <- f

/// Processes a WM_INPUT message to extract mouse movement deltas.
/// Allocates unmanaged memory to receive the raw input data, then
/// extracts and forwards the relative mouse movement to sendWheelRaw.
let private procRawInput (lParam:nativeint): unit =
    let mutable pcbSize = 0u
    let cbSizeHeader = uint32 <| Marshal.SizeOf(typeof<WinAPI.RAWINPUTHEADER>)

    let getRawInputData data =
        WinAPI.GetRawInputData(lParam, RID_INPUT, data, &pcbSize, cbSizeHeader)

    let isMouseMoveRelative (ri:WinAPI.RAWINPUT) =
        (ri.header.dwType = RIM_TYPEMOUSE) && (ri.mouse.usFlags = MOUSE_MOVE_RELATIVE)

    // First call with null gets required buffer size
    if (getRawInputData IntPtr.Zero) = 0u then
        let buf = Marshal.AllocHGlobal(int pcbSize)
        try
            // Second call retrieves the actual data
            if (getRawInputData buf) = pcbSize then
                let ri = Marshal.PtrToStructure(buf, typeof<WinAPI.RAWINPUT>) :?> WinAPI.RAWINPUT
                if isMouseMoveRelative ri then
                    let rm = ri.mouse
                    sendWheelRaw rm.lLastX rm.lLastY
        finally
            Marshal.FreeHGlobal(buf)

/// Message-only window that receives WM_INPUT messages.
/// Using HWND_MESSAGE as parent creates an invisible window that only receives messages.
type MessageWindow() =
    inherit NativeWindow()
    do
        let cp = CreateParams()
        cp.Parent <- HWND_MESSAGE
        base.CreateHandle(cp)

    override self.WndProc(m:Message byref): unit =
        if m.Msg = WM_INPUT then
            procRawInput(m.LParam)

        base.WndProc(&m)

/// Singleton message window instance
let private messageWindow = new MessageWindow()

/// Registers or unregisters for raw mouse input.
let private registerMouseRawInputDevice (dwFlags:uint32) (hwnd:nativeint) =
    let rid = [| WinAPI.RAWINPUTDEVICE(HID_USAGE_PAGE_GENERIC, HID_USAGE_GENERIC_MOUSE, dwFlags, hwnd) |]
    let ridSize = uint32 <| Marshal.SizeOf(typeof<WinAPI.RAWINPUTDEVICE>)
    WinAPI.RegisterRawInputDevices(rid, 1u, ridSize)

/// Registers to receive raw mouse input. Called when scroll mode starts.
let register () =
    if not (registerMouseRawInputDevice RIDEV_INPUTSINK messageWindow.Handle) then
        Dialog.errorMessage ("Failed register RawInput: " + (WinError.getLastErrorMessage())) "Error"

/// Unregisters from raw mouse input. Called when scroll mode ends.
let unregister () =
    if not (registerMouseRawInputDevice RIDEV_REMOVE IntPtr.Zero) then
        Dialog.errorMessage ("Failed unregister RawInput: " + (WinError.getLastErrorMessage())) "Error"
