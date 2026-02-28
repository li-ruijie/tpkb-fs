/// Single instance enforcement using a named mutex.
///
/// Uses a system mutex to prevent multiple instances of the application
/// from running simultaneously. This is important because multiple
/// instances would conflict when both try to install mouse hooks.
module PreventMultiInstance

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.IO
open System.Security.AccessControl
open System.Security.Principal
open System.Threading

/// Creates a MutexSecurity that grants access only to the current user,
/// with a Medium Mandatory Label for cross-elevation IPC.
let private createMutexSecurity () =
    let ms = MutexSecurity()
    let user = WindowsIdentity.GetCurrent().User
    ms.AddAccessRule(MutexAccessRule(user, MutexRights.FullControl, AccessControlType.Allow))
    try
        let sddl = ms.GetSecurityDescriptorSddlForm(AccessControlSections.Access)
        ms.SetSecurityDescriptorSddlForm(sddl + "S:(ML;;NW;;;ME)", AccessControlSections.Access ||| AccessControlSections.Audit)
    with _ -> ()
    ms

/// The named mutex used for single-instance enforcement.
let private mutex =
    let mutable createdNew = false
    try
        new Mutex(false, @"Local\tpkb_SingleInstance", &createdNew, createMutexSecurity())
    with
    | :? IOException ->
        createdNew <- false
        new Mutex(false, @"Local\tpkb_SingleInstance", &createdNew)

/// Whether this instance currently holds the mutex.
let mutable private locked = false

/// Attempts to acquire the mutex.
/// Returns true if lock acquired successfully, false if another instance holds it.
/// Throws InvalidOperationException if called when already locked.
let tryLock (): bool =
    if locked then
        raise (InvalidOperationException())

    locked <-
        try
            mutex.WaitOne(0)
        with
        | :? AbandonedMutexException -> true
    locked

/// Releases the mutex. Safe to call even if not locked.
/// Should be called during application exit to allow future instances.
let unlock () =
    if locked then
        mutex.ReleaseMutex()
        locked <- false
