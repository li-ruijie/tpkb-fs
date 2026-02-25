/// Windows error code utilities.
///
/// Retrieves the last Win32 error code and converts it to a human-readable
/// message using Win32Exception for display in error dialogs.
///
/// Used primarily when P/Invoke calls fail (e.g., SetWindowsHookEx,
/// RegisterRawInputDevices) to provide meaningful error messages to users.
module WinError

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System.Runtime.InteropServices
open System.ComponentModel

/// Gets the last Win32 error code set by a P/Invoke call.
/// Only valid immediately after a call that sets SetLastError = true.
let getLastErrorCode (): int =
    Marshal.GetLastWin32Error()

/// Gets a human-readable message for the last Win32 error.
/// Uses Win32Exception which knows how to format system error codes.
let getLastErrorMessage (): string =
    (new Win32Exception(getLastErrorCode())).Message

