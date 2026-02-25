/// Properties file management for loading and saving user settings.
///
/// Handles reading and writing key=value pairs from .properties files,
/// supporting multiple named profiles stored in the user's home directory.
///
/// File naming convention:
/// - Default: ~/.W10Wheel.properties
/// - Named profiles: ~/.W10Wheel.{name}.properties
///
/// Features:
/// - Sorted dictionary for consistent file ordering
/// - Comment stripping (lines with #)
/// - Type-safe getters for int, double, bool, and arrays
/// - Profile copy and delete operations
module Properties

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.IO
open System.Collections.Generic
open System.Diagnostics
open System.Text.RegularExpressions

let private propLineRegex = new Regex("^\s*\S+\s*=\s*\S+\s*$")

type Properties() =
    let sdict = SortedDictionary<string, string>()
    let trims (ss:string[]) = ss |> Array.map (fun s -> s.Trim())

    let removeComment (l:string) =
        match l.IndexOf('#') with
        | -1 -> l
        | n -> l.Substring(0, n)

    let isPropLine l = propLineRegex.Match(l).Success

    let getKeyValue (l:string) =
        match l.Split('=') |> trims with
        | [|k; v|] -> (k, v)
        | _ -> invalidArg "l" "Invalid property line"

    let setDict (k, v) =
        Debug.WriteLine(sprintf "Load property: %s = %s" k v)
        sdict.[k] <- v

    member self.Clear (): unit =
        sdict.Clear()

    member self.Load (path:string, update:bool): unit =
        if update || sdict.Count = 0 then
            File.ReadAllLines(path) |>
            Array.map removeComment |> trims |>
            Array.filter isPropLine |> Array.map getKeyValue |>
            Array.iter setDict

    member self.Store (path:string): unit =
        let makePropLine k = (sprintf "%s=%s" k sdict.[k])
        let lines = Seq.map makePropLine sdict.Keys
        File.WriteAllLines(path, lines)

    member self.GetProperty (key:string): string =
        try
            sdict.[key]
        with
            | :? KeyNotFoundException as e -> raise (KeyNotFoundException(key))

    member self.GetPropertyOption (key:string): string option =
        match sdict.TryGetValue key with
        | true, value -> Some(value)
        | _ -> None

    member self.SetProperty (key:string, value:string): unit =
        sdict.[key] <- value

    member self.Item
        with get key = self.GetProperty key
        and set key value = self.SetProperty(key, value)

    member self.GetString (key:string): string =
        self.GetProperty(key)

    member self.GetInt (key:string): int =
        match Int32.TryParse(self.GetString(key)) with
        | true, value -> value
        | false, _ -> raise (FormatException(sprintf "Invalid integer for key '%s'" key))

    member self.GetDouble (key:string): double =
        match Double.TryParse(self.GetString(key)) with
        | true, value -> value
        | false, _ -> raise (FormatException(sprintf "Invalid double for key '%s'" key))

    member self.GetBool (key:string): bool =
        match Boolean.TryParse(self.GetString(key)) with
        | true, value -> value
        | false, _ -> raise (FormatException(sprintf "Invalid boolean for key '%s'" key))

    member self.GetArray (key:string): string[] =
        self.GetString(key).Split(',') |> trims |>
        Array.filter (fun s -> s <> "")

    member self.GetIntArray (key:string): int[] =
        self.GetArray(key) |> Array.map (fun s ->
            match Int32.TryParse(s) with
            | true, value -> value
            | false, _ -> raise (FormatException(sprintf "Invalid integer '%s' in array for key '%s'" s key)))

    member self.GetDoubleArray (key:string): double[] =
        self.GetArray(key) |> Array.map (fun s ->
            match Double.TryParse(s) with
            | true, value -> value
            | false, _ -> raise (FormatException(sprintf "Invalid double '%s' in array for key '%s'" s key)))

    member self.SetInt (key:string, n:int): unit =
        self.SetProperty(key, n.ToString())

    member self.SetDouble (key:string, d:double): unit =
        self.SetProperty(key, d.ToString("F"))

    member self.SetBool (key:string, b:bool): unit =
        self.SetProperty(key, b.ToString())

        
let USER_DIR = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
let CONFIG_DIR = Path.Combine(USER_DIR, ".config", "w10wheel")
let PROP_BASE = "w10wheel"
let PROP_EXT = "conf"
let DEFAULT_PROP_NAME = (sprintf "%s.%s" PROP_BASE PROP_EXT)
let DEFAULT_DEF = "Default"

let private BAD_DEFAULT_NAME = (sprintf "%s.%s.%s" PROP_BASE DEFAULT_DEF PROP_EXT)

let private userDefRegex = new Regex(sprintf "^%s\.(?!--)(.+)\.%s$" PROP_BASE PROP_EXT)

let private isPropFile (path:String): bool =
    let name = Path.GetFileName(path)
    name <> BAD_DEFAULT_NAME && userDefRegex.Match(name).Success

let getUserDefName (path:String): string =
    let name = Path.GetFileName(path)
    userDefRegex.Match(name).Groups.[1].Value

let private ensureConfigDir () =
    if not (Directory.Exists(CONFIG_DIR)) then
        Directory.CreateDirectory(CONFIG_DIR) |> ignore

let getPropFiles (): string[] =
    ensureConfigDir()
    Directory.GetFiles(CONFIG_DIR) |> Array.filter isPropFile

let getDefaultPath (): string =
    ensureConfigDir()
    Path.Combine(CONFIG_DIR, DEFAULT_PROP_NAME)

let getPath (name:string): string =
    ensureConfigDir()
    if name = "Default" then
        getDefaultPath()
    else
        Path.Combine(CONFIG_DIR, (sprintf "%s.%s.%s" PROP_BASE name PROP_EXT))

let exists (name:string): bool =
    File.Exists(getPath name)

let copy (srcName:string) (destName:string): unit =
    let srcPath = getPath(srcName)
    let destPath = getPath(destName)

    if File.Exists(destPath) then
        raise (IOException(sprintf "Properties file already exists: %s" destName))

    File.Copy(srcPath, destPath)

let delete (name:string): unit =
    File.Delete(getPath name)

