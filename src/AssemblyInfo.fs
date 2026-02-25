/// Assembly metadata and version information for W10Wheel.NET.
/// This file contains .NET assembly attributes that define the application's
/// identity, version, and COM visibility settings. The version here is
/// automatically read at runtime by AppDef.fs to display in the UI.
namespace TestHook.AssemblyInfo

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

// Application identity - displayed in Windows properties dialog
[<assembly: AssemblyTitle("W10Wheel.NET")>]
[<assembly: AssemblyDescription("Mouse Wheel Simulator")>]
[<assembly: AssemblyConfiguration("")>]
[<assembly: AssemblyCompany("")>]
[<assembly: AssemblyProduct("W10Wheel.NET")>]
[<assembly: AssemblyCopyright("Copyright (c) 2016 Yuki Ono")>]
[<assembly: AssemblyTrademark("")>]
[<assembly: AssemblyCulture("")>]

// COM interop settings - not exposed to COM
[<assembly: ComVisible(false)>]

// Unique identifier for this assembly when exposed to COM
[<assembly: Guid("f7db64d0-fad4-4547-aa0a-09e24bb010ef")>]

// Version format: Major.Minor.Build.Revision
// Update these values when releasing a new version using release.bat
// AppDef.fs reads Major.Minor.Build at runtime for UI display
[<assembly: AssemblyVersion("2.8.0.0")>]
[<assembly: AssemblyFileVersion("2.8.0.0")>]

// Required empty do block for F# assembly-level attributes
do
    ()
