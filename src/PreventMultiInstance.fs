/// Single instance enforcement using file locking.
///
/// Creates a lock file in the temp directory to prevent multiple instances
/// of the application from running simultaneously. This is important because
/// multiple instances would conflict when both try to install mouse hooks.
///
/// The lock file approach is simpler than using a named mutex and works
/// reliably across Windows sessions. The file is opened with exclusive access
/// (FileShare.None), so any second instance attempting to open it will fail.
module PreventMultiInstance

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.IO
open System.Diagnostics
open System.Threading

/// Directory where the lock file is created (system temp folder)
let LOCK_FILE_DIR = Path.GetTempPath()

/// Lock file name derived from program name
let LOCK_FILE_NAME = AppDef.PROGRAM_NAME + ".lock"

/// Full path to the lock file (e.g., C:\Users\...\AppData\Local\Temp\W10Wheel.lock)
let LOCK_FILE_PATH = Path.Combine(LOCK_FILE_DIR, LOCK_FILE_NAME)

/// Holds the file stream that maintains the exclusive lock.
/// Using ref with Volatile for thread-safe access.
let private lock: FileStream ref = ref null

/// Checks if this instance currently holds the lock.
let isLocked (): bool =
    Volatile.Read(lock) <> null

/// Attempts to acquire the exclusive file lock.
/// Returns true if lock acquired successfully, false if another instance holds it.
/// Throws InvalidOperationException if called when already locked.
let tryLock (): bool =
    if isLocked() then
        raise (InvalidOperationException())

    try
        // Open with exclusive access - no sharing allowed
        Volatile.Write(lock, new FileStream(LOCK_FILE_PATH, FileMode.OpenOrCreate,
                                 FileAccess.ReadWrite, FileShare.None))
        true
    with
        | :? IOException -> false  // Another instance has the lock

/// Releases the file lock. Safe to call even if not locked.
/// Should be called during application exit to allow future instances.
let unlock () =
    if isLocked() then
        Volatile.Read(lock).Close()
