/// Application constants and version information.
/// Defines program name and version strings used throughout the application.
module AppDef

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System.Reflection

/// Short program name used for properties file naming (.W10Wheel.properties)
let PROGRAM_NAME = "W10Wheel"

/// Full program name displayed in UI (system tray tooltip, info dialog)
let PROGRAM_NAME_NET = "W10Wheel.NET"

/// Program version extracted from assembly metadata at runtime.
/// Format: Major.Minor (e.g., "2.8")
/// This avoids hardcoding the version in multiple places.
let PROGRAM_VERSION =
    let asm = Assembly.GetExecutingAssembly()
    let ver = asm.GetName().Version
    sprintf "%d.%d" ver.Major ver.Minor
