/// Properties file management for loading and saving user settings.
///
/// Handles reading and writing key=value pairs from .properties files,
/// supporting multiple named profiles stored in the user's home directory.
///
/// File naming convention:
/// - Default: ~/.config/tpkb/tpkb.ini
/// - Named profiles: ~/.config/tpkb/tpkb.{name}.ini
///
/// Features:
/// - Sorted dictionary for consistent file ordering
/// - Comment stripping (lines with #)
/// - Type-safe getters for int, double, bool, and arrays
/// - Profile copy and delete operations
module Properties

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Globalization
open System.IO
open System.Collections.Generic
open System.Diagnostics
open System.Text.RegularExpressions

type private IniMapping = { Section: string; IniKey: string; InternalKey: string }

let private iniMap = [|
    (* General *)
    { Section = "General"; IniKey = "trigger";               InternalKey = "firstTrigger" }
    { Section = "General"; IniKey = "send_middle_click";     InternalKey = "sendMiddleClick" }
    { Section = "General"; IniKey = "dragged_lock";          InternalKey = "draggedLock" }
    { Section = "General"; IniKey = "keyboard_hook";         InternalKey = "keyboardHook" }
    { Section = "General"; IniKey = "vk_code";               InternalKey = "targetVKCode" }
    { Section = "General"; IniKey = "priority";              InternalKey = "processPriority" }
    { Section = "General"; IniKey = "health_check_interval"; InternalKey = "hookHealthCheck" }
    (* Scroll *)
    { Section = "Scroll"; IniKey = "cursor_change";        InternalKey = "cursorChange" }
    { Section = "Scroll"; IniKey = "horizontal_scroll";    InternalKey = "horizontalScroll" }
    { Section = "Scroll"; IniKey = "reverse_scroll";       InternalKey = "reverseScroll" }
    { Section = "Scroll"; IniKey = "swap_scroll";          InternalKey = "swapScroll" }
    { Section = "Scroll"; IniKey = "poll_timeout";         InternalKey = "pollTimeout" }
    { Section = "Scroll"; IniKey = "scroll_lock_time";     InternalKey = "scrollLocktime" }
    { Section = "Scroll"; IniKey = "vertical_threshold";   InternalKey = "verticalThreshold" }
    { Section = "Scroll"; IniKey = "horizontal_threshold"; InternalKey = "horizontalThreshold" }
    { Section = "Scroll"; IniKey = "drag_threshold";       InternalKey = "dragThreshold" }
    (* Acceleration *)
    { Section = "Acceleration"; IniKey = "accel_table";             InternalKey = "accelTable" }
    { Section = "Acceleration"; IniKey = "multiplier";              InternalKey = "accelMultiplier" }
    { Section = "Acceleration"; IniKey = "custom_accel_table";      InternalKey = "customAccelTable" }
    { Section = "Acceleration"; IniKey = "custom_accel_threshold";  InternalKey = "customAccelThreshold" }
    { Section = "Acceleration"; IniKey = "custom_accel_multiplier"; InternalKey = "customAccelMultiplier" }
    (* Real Wheel *)
    { Section = "Real Wheel"; IniKey = "real_wheel_mode";  InternalKey = "realWheelMode" }
    { Section = "Real Wheel"; IniKey = "wheel_delta";      InternalKey = "wheelDelta" }
    { Section = "Real Wheel"; IniKey = "vertical_speed";   InternalKey = "vWheelMove" }
    { Section = "Real Wheel"; IniKey = "horizontal_speed"; InternalKey = "hWheelMove" }
    { Section = "Real Wheel"; IniKey = "quick_first";      InternalKey = "quickFirst" }
    { Section = "Real Wheel"; IniKey = "quick_turn";       InternalKey = "quickTurn" }
    (* VH Adjuster *)
    { Section = "VH Adjuster"; IniKey = "vh_adjuster_mode";   InternalKey = "vhAdjusterMode" }
    { Section = "VH Adjuster"; IniKey = "method";             InternalKey = "vhAdjusterMethod" }
    { Section = "VH Adjuster"; IniKey = "prefer_vertical";    InternalKey = "firstPreferVertical" }
    { Section = "VH Adjuster"; IniKey = "min_threshold";      InternalKey = "firstMinThreshold" }
    { Section = "VH Adjuster"; IniKey = "switching_threshold"; InternalKey = "switchingThreshold" }
    (* Keyboard *)
    { Section = "Keyboard"; IniKey = "character_repeat_delay";       InternalKey = "kbRepeatDelay" }
    { Section = "Keyboard"; IniKey = "character_repeat_speed";       InternalKey = "kbRepeatSpeed" }
    { Section = "Keyboard"; IniKey = "filter_keys";                  InternalKey = "filterKeys" }
    { Section = "Keyboard"; IniKey = "filter_keys_lock";             InternalKey = "fkLock" }
    { Section = "Keyboard"; IniKey = "filter_keys_acceptance_delay"; InternalKey = "fkAcceptanceDelay" }
    { Section = "Keyboard"; IniKey = "filter_keys_repeat_delay";     InternalKey = "fkRepeatDelay" }
    { Section = "Keyboard"; IniKey = "filter_keys_repeat_rate";      InternalKey = "fkRepeatRate" }
    { Section = "Keyboard"; IniKey = "filter_keys_bounce_time";      InternalKey = "fkBounceTime" }
|]

let private iniSections = [| "General"; "Scroll"; "Acceleration"; "Real Wheel"; "VH Adjuster"; "Keyboard" |]

let private iniToInternal (section: string) (iniKey: string): string option =
    iniMap |> Array.tryPick (fun m ->
        if m.Section = section && m.IniKey = iniKey then Some m.InternalKey else None)

type Properties() =
    let sdict = SortedDictionary<string, string>()
    let trims (ss:string[]) = ss |> Array.map (fun s -> s.Trim())

    member self.Clear (): unit =
        sdict.Clear()

    member self.Load (path:string, update:bool): unit =
        if update || sdict.Count = 0 then
            let mutable section = ""
            for line in File.ReadLines(path) do
                let trimmed = line.Trim()
                if trimmed.Length > 0 && trimmed.[0] <> '#' && trimmed.[0] <> ';' then
                    if trimmed.[0] = '[' then
                        let endIdx = trimmed.IndexOf(']')
                        if endIdx > 1 then
                            section <- trimmed.Substring(1, endIdx - 1)
                    elif section.Length > 0 then
                        let eqIdx = trimmed.IndexOf('=')
                        if eqIdx > 0 then
                            let k = trimmed.Substring(0, eqIdx).Trim()
                            let v = trimmed.Substring(eqIdx + 1).Trim()
                            if k.Length > 0 && v.Length > 0 then
                                match iniToInternal section k with
                                | Some internalKey ->
                                    Debug.WriteLine(sprintf "Load property: %s = %s" internalKey v)
                                    sdict.[internalKey] <- v
                                | None -> ()

    member self.Store (path:string): unit =
        let lines = ResizeArray<string>()
        for si in 0 .. iniSections.Length - 1 do
            let section = iniSections.[si]
            let entries = iniMap |> Array.filter (fun m ->
                m.Section = section && sdict.ContainsKey(m.InternalKey))
            if entries.Length > 0 then
                if lines.Count > 0 then lines.Add("")
                lines.Add(sprintf "[%s]" section)
                for m in entries do
                    lines.Add(sprintf "%s=%s" m.IniKey sdict.[m.InternalKey])
        let tmp = path + ".tmp"
        File.WriteAllLines(tmp, lines)
        if File.Exists(path) then
            File.Replace(tmp, path, null)
        else
            File.Move(tmp, path)

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
        match Int32.TryParse(self.GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture) with
        | true, value -> value
        | false, _ -> raise (FormatException(sprintf "Invalid integer for key '%s'" key))

    member self.GetDouble (key:string): double =
        match Double.TryParse(self.GetString(key), NumberStyles.Float, CultureInfo.InvariantCulture) with
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
            match Int32.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture) with
            | true, value -> value
            | false, _ -> raise (FormatException(sprintf "Invalid integer '%s' in array for key '%s'" s key)))

    member self.GetDoubleArray (key:string): double[] =
        self.GetArray(key) |> Array.map (fun s ->
            match Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture) with
            | true, value -> value
            | false, _ -> raise (FormatException(sprintf "Invalid double '%s' in array for key '%s'" s key)))

    member self.SetInt (key:string, n:int): unit =
        self.SetProperty(key, n.ToString(CultureInfo.InvariantCulture))

    member self.SetDouble (key:string, d:double): unit =
        self.SetProperty(key, d.ToString("F", CultureInfo.InvariantCulture))

    member self.SetBool (key:string, b:bool): unit =
        self.SetProperty(key, b.ToString())

        
let USER_DIR = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
let CONFIG_DIR = Path.Combine(USER_DIR, ".config", "tpkb")
let PROP_BASE = "tpkb"
let PROP_EXT = "ini"
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

