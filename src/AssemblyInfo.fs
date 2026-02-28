/// Assembly metadata and version information for tpkb.
/// This file contains .NET assembly attributes that define the application's
/// identity, version, and COM visibility settings. The version here is
/// automatically read at runtime by AppDef.fs to display in the UI.
namespace TestHook.AssemblyInfo

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System.Reflection
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

// Application identity - displayed in Windows properties dialog
[<assembly: AssemblyTitle("tpkb")>]
[<assembly: AssemblyDescription("TrackPoint Keyboard Helper")>]
[<assembly: AssemblyConfiguration("")>]
[<assembly: AssemblyCompany("")>]
[<assembly: AssemblyProduct("tpkb")>]
[<assembly: AssemblyCopyright("Copyright (c) 2026 Li Ruijie")>]
[<assembly: AssemblyTrademark("")>]
[<assembly: AssemblyCulture("")>]

// COM interop settings - not exposed to COM
[<assembly: ComVisible(false)>]

// Unique identifier for this assembly when exposed to COM
[<assembly: Guid("f7db64d0-fad4-4547-aa0a-09e24bb010ef")>]

// Version format: Major.Minor.Build.Revision
// Update these values when releasing a new version using release.bat
// AppDef.fs reads Major.Minor.Build at runtime for UI display
[<assembly: AssemblyVersion("3.0.1.0")>]
[<assembly: AssemblyFileVersion("3.0.1.0")>]

// Required empty do block for F# assembly-level attributes
do
    ()
