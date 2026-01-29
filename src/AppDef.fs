/// Application constants and version information.
/// Defines program name and version strings used throughout the application.
module AppDef

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System.Reflection

/// Program name used for properties file naming and UI display
let PROGRAM_NAME = "tpkb"

/// Program version extracted from assembly metadata at runtime.
/// Format: Major.Minor (e.g., "2.8")
/// This avoids hardcoding the version in multiple places.
let PROGRAM_VERSION =
    let asm = Assembly.GetExecutingAssembly()
    let ver = asm.GetName().Version
    sprintf "%d.%d" ver.Major ver.Minor
