/// Windows API P/Invoke declarations and data structures.
///
/// Contains all Win32 API imports required for W10Wheel's core functionality:
/// - Low-level mouse and keyboard hooks (WH_MOUSE_LL, WH_KEYBOARD_LL)
/// - SendInput for simulating mouse wheel events
/// - Cursor manipulation (SetSystemCursor, LoadCursor)
/// - Raw Input API for precise mouse movement tracking
/// - Process and window management functions
///
/// All structures use LayoutKind.Sequential to match C memory layout.
/// The #nowarn "9" suppresses warnings about native pointer usage in structs.
module WinAPI

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

#nowarn "9"  // Suppress "Uses of this construct may result in the generation of unverifiable .NET IL code"

open System
open System.Reflection
open System.Runtime.InteropServices

// ============================================================================
// Basic Structures
// ============================================================================

// https://msdn.microsoft.com/library/windows/desktop/dd162805.aspx
// http://www.pinvoke.net/default.aspx/Structures/POINT.html
/// Windows POINT structure representing x,y coordinates
[<Struct; StructLayout(LayoutKind.Sequential)>]
type POINT =
    val x: int32
    val y: int32

    new (_x, _y) = { x = _x; y = _y}

// ============================================================================
// Hook Structures - Data passed to low-level hook callbacks
// ============================================================================

// https://msdn.microsoft.com/library/windows/desktop/ms644970.aspx
// http://www.pinvoke.net/default.aspx/Structures/MSLLHOOKSTRUCT.html
/// Low-level mouse hook structure containing mouse event details.
/// Passed to LowLevelMouseProc callback by Windows.
[<StructLayout(LayoutKind.Sequential)>]
type MSLLHOOKSTRUCT =
    val pt          : POINT
    val mouseData   : uint32
    val flags       : uint32
    val time        : uint32
    val dwExtraInfo : unativeint  // Application-defined value (used for resend detection)

// https://msdn.microsoft.com/library/windows/desktop/ms644967.aspx
// http://pinvoke.net/default.aspx/Structures/KBDLLHOOKSTRUCT.html
/// Low-level keyboard hook structure containing key event details.
/// Passed to LowLevelKeyboardProc callback by Windows.
[<StructLayout(LayoutKind.Sequential)>]
type KBDLLHOOKSTRUCT =
    val vkCode      : uint32
    val scanCode    : uint32
    val flags       : uint32
    val time        : uint32
    val dwExtraInfo : unativeint

// ============================================================================
// SendInput Structures - Used to simulate mouse/keyboard input
// ============================================================================

// http://stackoverflow.com/questions/4177850/how-to-simulate-mouse-clicks-and-keypresses-in-f
// https://msdn.microsoft.com/library/windows/desktop/ms646273.aspx
// http://pinvoke.net/default.aspx/Structures/MOUSEINPUT.html
/// Mouse input structure for SendInput API.
/// Used to simulate mouse wheel events and button clicks.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type MOUSEINPUT =
    val dx          : int32
    val dy          : int32
    val mouseData   : uint32
    val dwFlags     : uint32
    val time        : uint32
    val dwExtraInfo : unativeint

    new(x, y, data, flags, _time, info) = {dx = x; dy = y; mouseData = data; dwFlags = flags; time = _time; dwExtraInfo = info}

// https://msdn.microsoft.com/library/ms646271.aspx
// http://pinvoke.net/default.aspx/Structures/KEYBDINPUT.html
/// Keyboard input structure for SendInput API (not currently used).
[<Struct; StructLayout(LayoutKind.Sequential)>]
type KEYBDINPUT =
    val wVk         : int16
    val wScan       : int16
    val dwFlags     : uint32
    val time        : uint32
    val dwExtraInfo : unativeint

/// Hardware input structure for SendInput API (not currently used).
[<Struct; StructLayout(LayoutKind.Sequential)>]
type HARDWAREINPUT =
    val uMsg    : int32
    val wParamL : int16
    val wParamH : int16

/// Wrapper for INPUT structure used by SendInput.
/// Type 0 = mouse input. Contains MOUSEINPUT union member.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type MINPUT =
    val ``type`` : uint32  // INPUT_MOUSE = 0
    val mi        : MOUSEINPUT

    new (_mi) = {``type`` = 0u; mi = _mi}

// ============================================================================
// Window Messages - Message IDs passed to hook callbacks
// ============================================================================

/// Windows message constants for mouse and keyboard events.
/// These are the wParam values received in low-level hook callbacks.
module Message =
    [<Literal>]
    let WM_KEYDOWN = 0x0100
    [<Literal>]
    let WM_KEYUP = 0x0101
    [<Literal>]
    let WM_SYSKEYDOWN = 0x0104
    [<Literal>]
    let WM_SYSKEYUP = 0x0105
    [<Literal>]
    let WM_MOUSEMOVE = 0x0200
    [<Literal>]
    let WM_LBUTTONDOWN = 0x0201
    [<Literal>]
    let WM_LBUTTONUP = 0x0202
    [<Literal>]
    let WM_LBUTTONDBLCLK = 0x0203
    [<Literal>]
    let WM_RBUTTONDOWN = 0x0204
    [<Literal>]
    let WM_RBUTTONUP = 0x0205
    [<Literal>]
    let WM_RBUTTONDBLCLK = 0x0206
    [<Literal>]
    let WM_MBUTTONDOWN = 0x207
    [<Literal>]
    let WM_MBUTTONUP = 0x208
    [<Literal>]
    let WM_MBUTTONDBLCLK = 0x0209
    [<Literal>]
    let WM_MOUSEWHEEL = 0x020A
    [<Literal>]
    let WM_XBUTTONDOWN = 0x020B
    [<Literal>]
    let WM_XBUTTONUP = 0x020C
    [<Literal>]
    let WM_XBUTTONDBLCLK = 0x020D
    [<Literal>]
    let WM_MOUSEHWHEEL = 0x020E  // Horizontal wheel scroll

// ============================================================================
// Hook Constants
// ============================================================================

/// Hook type for low-level keyboard hook
let WH_KEYBOARD_LL = 13

/// Hook type for low-level mouse hook
let WH_MOUSE_LL = 14

/// Standard wheel delta for one notch of rotation
let WHEEL_DELTA = 120

// X button identifiers (high-order word of mouseData)
let XBUTTON1 = 0x0001  // Back button
let XBUTTON2 = 0x0002  // Forward button

// ============================================================================
// Mouse Event Flags - Used with SendInput and mouse_event
// ============================================================================

/// Flags for mouse input events in SendInput/mouse_event calls.
module Event =
    let MOUSEEVENTF_ABSOLUTE = 0x8000
    let MOUSEEVENTF_HWHEEL = 0x01000
    let MOUSEEVENTF_MOVE = 0x0001
    let MOUSEEVENTF_LEFTDOWN = 0x0002
    let MOUSEEVENTF_LEFTUP = 0x0004
    let MOUSEEVENTF_RIGHTDOWN = 0x0008
    let MOUSEEVENTF_RIGHTUP = 0x0010
    let MOUSEEVENTF_MIDDLEDOWN = 0x0020
    let MOUSEEVENTF_MIDDLEUP = 0x0040
    let MOUSEEVENTF_WHEEL = 0x0800
    let MOUSEEVENTF_XDOWN = 0x0080
    let MOUSEEVENTF_XUP = 0x0100

// ============================================================================
// Hook Callback Delegates
// ============================================================================

// https://msdn.microsoft.com/library/windows/desktop/ms644986.aspx
// http://www.pinvoke.net/default.aspx/Delegates/LowLevelMouseProc.html
/// Delegate for low-level mouse hook callback function.
/// nCode: Hook code (if < 0, must call CallNextHookEx)
/// wParam: Message type (WM_MOUSEMOVE, WM_LBUTTONDOWN, etc.)
/// lParam: MSLLHOOKSTRUCT containing mouse event details
type LowLevelMouseProc = delegate of nCode: int * wParam: nativeint * [<In>]lParam: MSLLHOOKSTRUCT -> nativeint

// https://msdn.microsoft.com/library/windows/desktop/ms644985.aspx
// http://pinvoke.net/default.aspx/Delegates/LowLevelKeyboardProc.html
/// Delegate for low-level keyboard hook callback function.
type LowLevelKeyboardProc = delegate of nCode: int * wParam: nativeint * [<In>]lParam: KBDLLHOOKSTRUCT -> nativeint

// ============================================================================
// P/Invoke Declarations - Kernel32
// ============================================================================

[<DllImport("kernel32.dll")>]
extern uint32 GetCurrentThreadId()

// http://www.pinvoke.net/default.aspx/kernel32/GetModuleHandle.html
[<DllImport("kernel32.dll", SetLastError = true)>]
extern nativeint GetModuleHandle(string lpModuleName)

// ============================================================================
// P/Invoke Declarations - User32 (Hooks)
// ============================================================================

// http://www.pinvoke.net/default.aspx/user32/UnhookWindowsHookEx.html
// https://msdn.microsoft.com/library/windows/desktop/ms644993.aspx
/// Removes a hook procedure installed by SetWindowsHookEx.
[<DllImport("user32.dll", SetLastError = true)>]
extern bool UnhookWindowsHookEx(nativeint hhk)

// https://msdn.microsoft.com/library/windows/desktop/ms644974.aspx
// http://www.pinvoke.net/default.aspx/user32.callnexthookex
/// Passes hook information to the next hook in the chain (mouse version).
[<DllImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)>]
extern nativeint CallNextHookExM(nativeint hhk, int32 nCode, nativeint wParam, [<In>]MSLLHOOKSTRUCT lParam)

/// Passes hook information to the next hook in the chain (keyboard version).
[<DllImport("user32.dll", EntryPoint = "CallNextHookEx", SetLastError = true)>]
extern nativeint CallNextHookExK(nativeint hhk, int32 nCode, nativeint wParam, [<In>]KBDLLHOOKSTRUCT lParam)

// http://pinvoke.net/default.aspx/user32/SetWindowsHookEx.html
// https://msdn.microsoft.com/library/windows/desktop/ms644990.aspx
/// Installs a low-level mouse hook procedure (mouse version).
[<DllImport("user32.dll", EntryPoint = "SetWindowsHookEx", SetLastError = true)>]
extern nativeint SetWindowsHookExM(int idHook, LowLevelMouseProc proc, nativeint hMod, uint32 dwThreadId)

/// Installs a low-level keyboard hook procedure (keyboard version).
[<DllImport("user32.dll", EntryPoint = "SetWindowsHookEx", SetLastError = true)>]
extern nativeint SetWindowsHookExK(int idHook, LowLevelKeyboardProc proc, nativeint hMod, uint32 dwThreadId)


// ============================================================================
// P/Invoke Declarations - User32 (Input)
// ============================================================================

// https://msdn.microsoft.com/library/windows/desktop/ms646310.aspx
// http://pinvoke.net/default.aspx/user32/SendInput.html
/// Synthesizes mouse input events. Used to send wheel scroll events.
[<DllImport("user32.dll", SetLastError = true)>]
extern uint32 SendInput(uint32 nInputs,
    [<MarshalAs(UnmanagedType.LPArray); In>]MINPUT[] pInputs, int cbSize)

/// Legacy mouse input function (not used, prefer SendInput).
[<DllImport("user32.dll", SetLastError = true)>]
extern void mouse_event(int32 dwFlags, int32 dx, int32 dy, int32 dwData, unativeint dwExtraInfo)

/// Gets extra info associated with the current input message.
[<DllImport("user32.dll", SetLastError = false)>]
extern nativeint GetMessageExtraInfo()

// ============================================================================
// Virtual Key Codes - For detecting modifier key states
// ============================================================================

/// Virtual key codes for modifier keys.
module VKey =
    let VK_SHIFT = 0x10
    let VK_CONTROL = 0x11
    let VK_MENU = 0x12
    let VK_ESCAPE = 0x1B

// https://msdn.microsoft.com/ibrary/windows/desktop/ms646293.aspx
// http://www.pinvoke.net/default.aspx/user32.getasynckeystate
/// Checks if a key is currently pressed (asynchronous state check).
[<DllImport("user32.dll")>]
extern int16 GetAsyncKeyState(int32 vKey)

// ============================================================================
// Cursor IDs - System cursor identifiers for cursor manipulation
// ============================================================================

/// System cursor identifiers used with LoadCursor/SetSystemCursor.
module CursorID =
    let OCR_NORMAL = 32512
    let OCR_IBEAM = 32513
    let OCR_HAND = 32649
    let OCR_SIZEALL = 32646
    let OCR_SIZENESW = 32643
    let OCR_SIZENS = 32645
    let OCR_SIZENWSE = 32642
    let OCR_SIZEWE = 32644
    let OCR_WAIT = 32514

// ============================================================================
// P/Invoke Declarations - User32 (Cursor)
// ============================================================================

// https://msdn.microsoft.com/library/windows/desktop/ms648391.aspx
// http://www.pinvoke.net/default.aspx/user32.loadcursor
/// Loads a system cursor by ID.
[<DllImport("user32.dll")>]
extern nativeint LoadCursor(nativeint hInstance, nativeint lpCursorName)

// LoadImage flags
let IMAGE_CURSOR = 2
let LR_DEFAULTSIZE = 0x00000040
let LR_SHARED = 0x00008000

// https://msdn.microsoft.com/library/windows/desktop/ms648045.aspx
// http://www.pinvoke.net/default.aspx/user32/LoadImage.html
/// Loads a cursor/icon/bitmap from system resources.
[<DllImport("user32.dll", SetLastError = true)>]
extern nativeint LoadImage(nativeint hinst, nativeint lpszName, uint32 uType, int32 cxDesired, int32 cyDesired, uint32 fuLoad)

// https://msdn.microsoft.com/library/windows/desktop/ms648058.aspx
// http://www.pinvoke.net/default.aspx/user32.copyicon
/// Creates a copy of a cursor. Required before SetSystemCursor (which destroys the cursor).
[<DllImport("user32.dll")>]
extern nativeint CopyIcon(nativeint hIcon)

// https://msdn.microsoft.com/library/windows/desktop/ms648395.aspx
// http://www.pinvoke.net/default.aspx/user32/SetSystemCursor.html
/// Replaces a system cursor. The cursor handle is destroyed after this call.
[<DllImport("user32.dll")>]
extern bool SetSystemCursor(nativeint hCur, uint32 id)

/// SystemParametersInfo action to reload all system cursors from registry.
let SPI_SETCURSORS = 0x0057

// https://msdn.microsoft.com/library/windows/desktop/ms724947.aspx
// http://www.pinvoke.net/default.aspx/user32.systemparametersinfo
/// Queries or sets system-wide parameters. Used to restore default cursors.
[<DllImport("user32.dll", SetLastError = true)>]
extern bool SystemParametersInfo(uint32 uiAction, uint32 uiParam, nativeint pvParam, uint32 fWinIni)

// ============================================================================
// Raw Input API - For precise mouse movement tracking
// ============================================================================

/// Raw Input constants and message identifiers.
module RawInput =
    let WM_INPUT = 0x00ff
    let HWND_MESSAGE = IntPtr(-3)
    let RID_INPUT = 0x10000003u
    let MOUSE_MOVE_RELATIVE = 0us
    let RIM_TYPEMOUSE = 0u
    let HID_USAGE_PAGE_GENERIC = 0x01us
    let HID_USAGE_GENERIC_MOUSE = 0x02us
    let RIDEV_INPUTSINK = 0x00000100u
    let RIDEV_REMOVE = 0x00000001u

/// Raw input device registration structure.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type RAWINPUTDEVICE =
    val usUsagePage : uint16
    val usUsage     : uint16
    val dwFlags     : uint32
    val hwndTarget  : nativeint

    new(usup, usu, dwf, ht) = {usUsagePage = usup; usUsage = usu; dwFlags = dwf; hwndTarget = ht}

/// Raw input message header.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type RAWINPUTHEADER =
    val dwType  : uint32
    val dwSize  : uint32
    val hDevice : nativeint
    val wParam  : nativeint

/// Raw mouse input data containing relative movement deltas.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type RAWMOUSE =
    val usFlags            : uint16
    val usButtonFlags      : uint16
    val usButtonData       : uint16
    val ulRawButtons       : uint32
    val lLastX             : int
    val lLastY             : int
    val ulExtraInformation : uint32

/// Combined raw input structure with header and mouse data.
[<Struct; StructLayout(LayoutKind.Sequential)>]
type RAWINPUT =
    val header : RAWINPUTHEADER
    val mouse  : RAWMOUSE

/// Registers a raw input device to receive WM_INPUT messages.
[<DllImport("user32.dll", SetLastError = true)>]
extern bool RegisterRawInputDevices([<MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0s); In>]RAWINPUTDEVICE[] pRawInputDevices, [<In>]uint32 uiNumDevices, [<In>]uint32 cbSize)

/// Retrieves raw input data from the specified handle.
[<DllImport("user32.dll", SetLastError = true)>]
extern uint32 GetRawInputData([<In>]nativeint hRawInput, [<In>]uint32 uiCommand, [<Out>]nativeint pData, [<In; Out>]uint32& pcbSize, [<In>]uint32 cbSizeHeader)

// ============================================================================
// P/Invoke Declarations - Window/Process Information
// ============================================================================

/// Gets the window handle at the specified screen coordinates.
[<DllImport("user32.dll", SetLastError = true)>]
extern nativeint WindowFromPoint(POINT point)

/// Gets the thread and process ID for a window.
[<DllImport("user32.dll", SetLastError = true)>]
extern uint32 GetWindowThreadProcessId(nativeint hWnd, [<Out>]uint32& lpdwProcessId)

/// Opens a process handle for querying information.
[<DllImport("kernel32.dll")>]
extern nativeint OpenProcess(uint32 dwDesiredAccess, bool bInheritHandle, uint32 dwProcessId)

/// Closes a kernel object handle.
[<DllImport("kernel32.dll")>]
extern bool CloseHandle(nativeint handle)

/// Gets the base name of a module (not currently used).
[<DllImport("psapi.dll", CharSet = CharSet.Ansi)>]
extern uint32 GetModuleBaseName(nativeint hWnd, nativeint hModule, [<MarshalAs(UnmanagedType.LPStr); Out>] System.Text.StringBuilder lpBaseName, uint32 nSize)

/// Gets the full path of a module (not currently used).
[<DllImport("psapi.dll")>]
extern uint32 GetModuleFileNameEx(nativeint hProcess, nativeint hModule, [<Out>] System.Text.StringBuilder lpBaseName, [<In>] [<MarshalAs(UnmanagedType.U4)>] int32 nSize)

/// Gets the full path of the executable for a process.
[<DllImport("kernel32.dll", SetLastError=true)>]
extern bool QueryFullProcessImageName([<In>]nativeint hProcess, [<In>]int32 dwFlags, [<Out>]System.Text.StringBuilder lpExeName, int32& lpdwSize)

/// Gets the handle of the currently active foreground window.
[<DllImport("user32.dll")>]
extern nativeint GetForegroundWindow()

/// Gets the current cursor position in screen coordinates.
[<DllImport("user32.dll", SetLastError = true)>]
extern [<MarshalAs(UnmanagedType.Bool)>] bool GetCursorPos(POINT& lpPoint);
