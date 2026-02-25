/// System cursor manipulation for visual scroll mode feedback.
///
/// Changes the system cursor to vertical or horizontal resize arrows
/// during scroll mode and restores the original cursors on exit.
/// This provides visual feedback to the user indicating scroll direction.
///
/// Note: SetSystemCursor destroys the cursor handle passed to it,
/// so we must copy the cursor before each call.
module WinCursor

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop

open WinAPI.CursorID

/// Loads a system cursor by its OCR_* identifier.
let private load (id: int) =
    WinAPI.LoadImage(IntPtr.Zero, IntPtr(id), uint32 WinAPI.IMAGE_CURSOR, 0, 0,
                     uint32 (WinAPI.LR_DEFAULTSIZE ^^^ WinAPI.LR_SHARED))

/// Pre-loaded vertical resize cursor (up-down arrows)
let CURSOR_V = load OCR_SIZENS

/// Pre-loaded horizontal resize cursor (left-right arrows)
let CURSOR_H = load OCR_SIZEWE

/// Creates a copy of a cursor handle.
/// Required because SetSystemCursor destroys the cursor passed to it.
let private copy (hCur:nativeint): nativeint =
    WinAPI.CopyIcon(hCur)

/// Replaces the normal, I-beam, and hand cursors with the specified cursor.
/// These are the most commonly visible cursors during normal usage.
let change (hCur: nativeint) =
    WinAPI.SetSystemCursor(copy(hCur), uint32 OCR_NORMAL) |> ignore
    WinAPI.SetSystemCursor(copy(hCur), uint32 OCR_IBEAM) |> ignore
    WinAPI.SetSystemCursor(copy(hCur), uint32 OCR_HAND) |> ignore

/// Changes system cursors to vertical scroll indicator (up-down arrows).
let changeV () =
    change(CURSOR_V)

/// Changes system cursors to horizontal scroll indicator (left-right arrows).
let changeH () =
    change(CURSOR_H)

/// Restores all system cursors to their default values.
/// Uses SPI_SETCURSORS which reloads cursors from the registry.
let restore () =
    WinAPI.SystemParametersInfo(uint32 WinAPI.SPI_SETCURSORS, 0u, IntPtr.Zero, 0u)
