/// Settings dialog with tabbed interface for all configuration options.
///
/// Uses WPF (PresentationFramework) for DPI-independent rendering.
/// Six tabs: General, Scroll, Acceleration, Real Wheel, VH Adjuster, Profiles.
/// Each tab follows init/apply/reset pattern matching the C implementation.
module Settings

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Diagnostics
open System.Reflection
open System.Windows
open System.Windows.Controls
open System.Windows.Controls.Primitives
open System.Windows.Media
open System.Windows.Media.Imaging
open System.Globalization

// ========== Help text ==========

let private HELP_GENERAL =
    "Trigger: Which mouse button(s) activate scroll mode.\r\n" +
    "  LR = hold Left then Right (or vice versa) to scroll.\r\n" +
    "  Drag variants = hold button and drag to scroll.\r\n" +
    "  None = use keyboard trigger only.\r\n\r\n" +
    "Send MiddleClick: Send a middle-click when the trigger\r\n" +
    "  button is released without scrolling (single/double triggers).\r\n\r\n" +
    "Dragged Lock: Keep scroll mode active after releasing\r\n" +
    "  the trigger button (drag triggers only). Click to exit.\r\n\r\n" +
    "Hotkey: Enable a keyboard key as scroll mode trigger.\r\n" +
    "  VK Code: which key to use (e.g., VK_SCROLL = Scroll Lock).\r\n\r\n" +
    "Priority: Process priority. Higher = more responsive scrolling\r\n" +
    "  but uses more CPU.\r\n\r\n" +
    "Health check interval: Seconds between hook health checks\r\n" +
    "  (0 = off). Monitors cursor movement to detect when Windows\r\n" +
    "  silently drops the mouse hook, and reinstalls it\r\n" +
    "  automatically. Does not affect power management or sleep."

let private HELP_SCROLL =
    "Cursor Change: Change the mouse cursor while in scroll mode\r\n" +
    "  to indicate scroll direction.\r\n\r\n" +
    "Horizontal Scroll: Allow horizontal scrolling by moving\r\n" +
    "  the mouse left/right while in scroll mode.\r\n\r\n" +
    "Reverse Scroll: Invert the vertical scroll direction\r\n" +
    "  (natural/reverse scrolling).\r\n\r\n" +
    "Swap Scroll (V/H): Swap vertical and horizontal scroll axes.\r\n\r\n" +
    "Button press timeout (50\u2013500 ms): Time window to detect\r\n" +
    "  simultaneous button presses for LR/Left/Right triggers.\r\n\r\n" +
    "Scroll lock time (150\u2013500 ms): Minimum time before scroll\r\n" +
    "  mode can be exited. Prevents accidental exits.\r\n\r\n" +
    "Vertical threshold (0\u2013500): Minimum mouse movement in\r\n" +
    "  pixels before vertical scrolling starts.\r\n\r\n" +
    "Horizontal threshold (0\u2013500): Same for horizontal scrolling.\r\n\r\n" +
    "Drag threshold (0\u2013500): Mouse movement threshold\r\n" +
    "  to distinguish drag-scroll from a click (drag triggers)."

let private HELP_ACCEL =
    "Enable acceleration: Turn on scroll acceleration.\r\n" +
    "  Faster mouse movement = larger scroll steps.\r\n\r\n" +
    "Preset (M5\u2013M9): Kensington MouseWorks-style multiplier\r\n" +
    "  tables. M5 is gentlest, M9 is most aggressive.\r\n\r\n" +
    "Use custom table: Override presets with your own values.\r\n" +
    "  Thresholds: comma-separated positive integers (mouse\r\n" +
    "  speed breakpoints in pixels).\r\n" +
    "  E.g.: 1,2,3,5,7,10\r\n" +
    "  Multipliers: comma-separated positive decimals\r\n" +
    "  (scroll multiplier at each speed; 1.0 = normal).\r\n" +
    "  E.g.: 1.0,1.5,2.0,2.5,3.0,3.5\r\n\r\n" +
    "  Both arrays must have the same number of entries.\r\n" +
    "  Maximum 64 entries per array."

let private HELP_REALWHEEL =
    "Enable real wheel mode: Simulate actual mouse wheel events\r\n" +
    "  instead of using SendInput. Some apps respond better to\r\n" +
    "  real wheel messages.\r\n\r\n" +
    "Wheel delta (10\u2013500): Size of each scroll step.\r\n" +
    "  Standard mouse wheel = 120. Smaller = smoother scrolling.\r\n\r\n" +
    "Vertical speed (10\u2013500): Vertical scroll speed\r\n" +
    "  (higher = faster).\r\n\r\n" +
    "Horizontal speed (10\u2013500): Horizontal scroll speed.\r\n\r\n" +
    "Quick first scroll: Make the first scroll event fire\r\n" +
    "  immediately instead of waiting for a threshold.\r\n" +
    "  Feels more responsive.\r\n\r\n" +
    "Quick direction change: Respond instantly when scroll\r\n" +
    "  direction changes. Useful for quickly switching between\r\n" +
    "  scrolling up and down."

let private HELP_VHADJ =
    "Enable VH adjuster: Constrain scrolling to vertical-only\r\n" +
    "  or horizontal-only based on initial movement direction.\r\n" +
    "  Prevents diagonal wobble. Requires Horizontal Scroll on.\r\n\r\n" +
    "Fixed: Lock to the first detected direction (V or H)\r\n" +
    "  for the entire scroll session.\r\n\r\n" +
    "Switching: Allow direction to switch if mouse movement\r\n" +
    "  changes axis. More flexible but may feel less precise.\r\n\r\n" +
    "Prefer vertical first: When initial movement is ambiguous,\r\n" +
    "  prefer vertical scrolling over horizontal.\r\n\r\n" +
    "Min. threshold (1\u201310): Minimum movement in pixels\r\n" +
    "  before locking direction. Higher = more deliberate.\r\n\r\n" +
    "Switching threshold (10\u2013500): Movement required to switch\r\n" +
    "  direction (Switching mode only)."

// ========== WPF Helpers ==========

let private addLabel (panel: StackPanel) text =
    let lbl = TextBlock(Text = text, Margin = Thickness(0.0, 0.0, 8.0, 0.0), VerticalAlignment = VerticalAlignment.Center)
    panel.Children.Add(lbl) |> ignore
    lbl

let private addCheckBox (parent: Panel) text (isChecked: bool) =
    let cb = CheckBox(Content = text, IsChecked = Nullable<bool>(isChecked), Margin = Thickness(0.0, 4.0, 0.0, 0.0))
    parent.Children.Add(cb) |> ignore
    cb

let private addComboBox (parent: Panel) (items: string[]) (selectedIndex: int) =
    let cb = ComboBox(MinWidth = 200.0, Margin = Thickness(0.0, 4.0, 0.0, 0.0))
    for item in items do
        cb.Items.Add(item) |> ignore
    if selectedIndex >= 0 && selectedIndex < items.Length then
        cb.SelectedIndex <- selectedIndex
    parent.Children.Add(cb) |> ignore
    cb

let private addSpinRow (parent: Panel) label (lo: int) (hi: int) (value: int) =
    let row = StackPanel(Orientation = Orientation.Horizontal, Margin = Thickness(0.0, 4.0, 0.0, 0.0))
    let lbl = TextBlock(Text = label, Width = 160.0, VerticalAlignment = VerticalAlignment.Center)
    row.Children.Add(lbl) |> ignore
    let tb = TextBox(Text = string value, Width = 70.0, VerticalAlignment = VerticalAlignment.Center)
    row.Children.Add(tb) |> ignore
    let info = TextBlock(Text = sprintf "  (%d\u2013%d)" lo hi, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray)
    row.Children.Add(info) |> ignore
    parent.Children.Add(row) |> ignore
    tb

let private addComboRow (parent: Panel) label (items: string[]) (selectedIndex: int) =
    let row = StackPanel(Orientation = Orientation.Horizontal, Margin = Thickness(0.0, 4.0, 0.0, 0.0))
    let lbl = TextBlock(Text = label, Width = 160.0, VerticalAlignment = VerticalAlignment.Center)
    row.Children.Add(lbl) |> ignore
    let cb = ComboBox(MinWidth = 200.0)
    for item in items do
        cb.Items.Add(item) |> ignore
    if selectedIndex >= 0 && selectedIndex < items.Length then
        cb.SelectedIndex <- selectedIndex
    row.Children.Add(cb) |> ignore
    parent.Children.Add(row) |> ignore
    cb

let private getSpinValue (tb: TextBox) lo hi name =
    match Int32.TryParse(tb.Text.Trim()) with
    | true, v when v >= lo && v <= hi -> Some v
    | _ ->
        MessageBox.Show(sprintf "%s: %d \u2013 %d" name lo hi, "Invalid Number", MessageBoxButton.OK, MessageBoxImage.Warning) |> ignore
        None

let private showHelp helpText =
    MessageBox.Show(helpText, "Help", MessageBoxButton.OK, MessageBoxImage.Information) |> ignore

// ========== Constants ==========

let private priorityNames = [| "High"; "Above Normal"; "Normal" |]
let private priorityIds = [| "High"; "AboveNormal"; "Normal" |]
let private presetNames = [| "M5"; "M6"; "M7"; "M8"; "M9" |]

let private formatAccelArrays (presetName: string) =
    let mul = Ctx.getAccelMultiplierOfName presetName
    let thr = Ctx.DefaultAccelThreshold
    let thrStr = thr |> Array.map string |> String.concat ","
    let mulStr = mul.DArray |> Array.map (fun d -> d.ToString("F1", CultureInfo.InvariantCulture)) |> String.concat ","
    (thrStr, mulStr)

// ========== Page: General ==========

type private GeneralControls = {
    TriggerCombo: ComboBox
    SendMiddle: CheckBox
    DraggedLock: CheckBox
    KbEnable: CheckBox
    VkCombo: ComboBox
    PrioCombo: ComboBox
    HealthSpin: TextBox
}

let private createGeneralPage () =
    let panel = StackPanel(Margin = Thickness(8.0))

    let triggerCombo = addComboRow panel "Trigger" Ctx.TRIGGER_NAMES (Ctx.getFirstTriggerIndex())
    let sendMiddle = addCheckBox panel "Send MiddleClick" (Ctx.getBooleanOfName DataID.sendMiddleClick)
    let draggedLock = addCheckBox panel "Dragged Lock" (Ctx.getBooleanOfName DataID.draggedLock)
    let kbEnable = addCheckBox panel "Hotkey" (Ctx.getBooleanOfName DataID.keyboardHook)
    let vkCombo = addComboRow panel "VK Code" Ctx.VK_NAMES (Ctx.getTargetVKCodeIndex())

    let curPrio = Ctx.getProcessPriorityName()
    let prioIdx = priorityIds |> Array.tryFindIndex (fun id -> id = curPrio) |> Option.defaultValue 1
    let prioCombo = addComboRow panel "Priority" priorityNames prioIdx

    let healthSpin = addSpinRow panel "Health check interval" 0 300 (Ctx.getNumberOfName DataID.hookHealthCheck)

    let controls = {
        TriggerCombo = triggerCombo; SendMiddle = sendMiddle; DraggedLock = draggedLock
        KbEnable = kbEnable; VkCombo = vkCombo; PrioCombo = prioCombo
        HealthSpin = healthSpin
    }
    (panel, controls)

let private applyGeneral (c: GeneralControls) =
    match getSpinValue c.HealthSpin 0 300 "Health check interval" with
    | None -> false
    | Some health ->
        let sel = c.TriggerCombo.SelectedIndex
        if sel >= 0 && sel < Ctx.TRIGGER_NAMES.Length then
            Ctx.setTrigger(Ctx.getTriggerIdOfIndex sel)

        Ctx.setBooleanOfName DataID.sendMiddleClick (c.SendMiddle.IsChecked.GetValueOrDefault())
        Ctx.setBooleanOfName DataID.draggedLock (c.DraggedLock.IsChecked.GetValueOrDefault())
        Ctx.setBooleanOfName DataID.keyboardHook (c.KbEnable.IsChecked.GetValueOrDefault())
        WinHook.setOrUnsetKeyboardHook (c.KbEnable.IsChecked.GetValueOrDefault())

        let vkSel = c.VkCombo.SelectedIndex
        if vkSel >= 0 && vkSel < Ctx.VK_NAMES.Length then
            Ctx.setTargetVKCode(Ctx.getVKIdOfIndex vkSel)

        let prioSel = c.PrioCombo.SelectedIndex
        if prioSel >= 0 && prioSel < priorityIds.Length then
            Ctx.setPriority priorityIds.[prioSel]

        Ctx.setNumberOfName DataID.hookHealthCheck health
        Ctx.updateHealthTimer()
        true

// ========== Page: Scroll ==========

type private ScrollControls = {
    CursorChange: CheckBox; HScroll: CheckBox; ReverseScroll: CheckBox; SwapScroll: CheckBox
    PollSpin: TextBox; LockSpin: TextBox; VertSpin: TextBox; HorizSpin: TextBox; DragSpin: TextBox
}

let private createScrollPage () =
    let panel = StackPanel(Margin = Thickness(8.0))

    let cursorChange = addCheckBox panel "Cursor Change" (Ctx.getBooleanOfName DataID.cursorChange)
    let hScroll = addCheckBox panel "Horizontal Scroll" (Ctx.getBooleanOfName DataID.horizontalScroll)
    let reverseScroll = addCheckBox panel "Reverse Scroll (Flip)" (Ctx.getBooleanOfName DataID.reverseScroll)
    let swapScroll = addCheckBox panel "Swap Scroll (V.H)" (Ctx.getBooleanOfName DataID.swapScroll)

    let sep = System.Windows.Controls.Separator(Margin = Thickness(0.0, 8.0, 0.0, 4.0))
    panel.Children.Add(sep) |> ignore

    let pollSpin = addSpinRow panel "Button press timeout" 50 500 (Ctx.getNumberOfName DataID.pollTimeout)
    let lockSpin = addSpinRow panel "Scroll lock time" 150 500 (Ctx.getNumberOfName DataID.scrollLocktime)
    let vertSpin = addSpinRow panel "Vertical threshold" 0 500 (Ctx.getNumberOfName DataID.verticalThreshold)
    let horizSpin = addSpinRow panel "Horizontal threshold" 0 500 (Ctx.getNumberOfName DataID.horizontalThreshold)
    let dragSpin = addSpinRow panel "Drag threshold" 0 500 (Ctx.getNumberOfName DataID.dragThreshold)

    let controls = {
        CursorChange = cursorChange; HScroll = hScroll; ReverseScroll = reverseScroll; SwapScroll = swapScroll
        PollSpin = pollSpin; LockSpin = lockSpin; VertSpin = vertSpin; HorizSpin = horizSpin; DragSpin = dragSpin
    }
    (panel, controls)

let private applyScroll (c: ScrollControls) =
    let vals = [|
        getSpinValue c.PollSpin 50 500 "Button press timeout"
        getSpinValue c.LockSpin 150 500 "Scroll lock time"
        getSpinValue c.VertSpin 0 500 "Vertical threshold"
        getSpinValue c.HorizSpin 0 500 "Horizontal threshold"
        getSpinValue c.DragSpin 0 500 "Drag threshold"
    |]
    if vals |> Array.exists Option.isNone then false
    else
        Ctx.setBooleanOfName DataID.cursorChange (c.CursorChange.IsChecked.GetValueOrDefault())
        Ctx.setBooleanOfName DataID.horizontalScroll (c.HScroll.IsChecked.GetValueOrDefault())
        Ctx.setBooleanOfName DataID.reverseScroll (c.ReverseScroll.IsChecked.GetValueOrDefault())
        Ctx.setBooleanOfName DataID.swapScroll (c.SwapScroll.IsChecked.GetValueOrDefault())
        Ctx.setNumberOfName DataID.pollTimeout vals.[0].Value
        Ctx.setNumberOfName DataID.scrollLocktime vals.[1].Value
        Ctx.setNumberOfName DataID.verticalThreshold vals.[2].Value
        Ctx.setNumberOfName DataID.horizontalThreshold vals.[3].Value
        Ctx.setNumberOfName DataID.dragThreshold vals.[4].Value
        true

// ========== Page: Acceleration ==========

type private AccelControls = {
    Enable: CheckBox; Radios: RadioButton[]; CustomEnable: CheckBox
    ThrBox: TextBox; MulBox: TextBox
}

let private createAccelPage () =
    let panel = StackPanel(Margin = Thickness(8.0))

    let accelEnable = addCheckBox panel "Enable" (Ctx.getBooleanOfName DataID.accelTable)

    let radioPanel = StackPanel(Orientation = Orientation.Horizontal, Margin = Thickness(0.0, 4.0, 0.0, 0.0))
    let radios =
        presetNames |> Array.map (fun name ->
            let rb = RadioButton(Content = name, Margin = Thickness(0.0, 0.0, 12.0, 0.0))
            radioPanel.Children.Add(rb) |> ignore
            rb
        )
    panel.Children.Add(radioPanel) |> ignore

    let presetName = Ctx.getAccelPresetName()
    let presetIdx = presetNames |> Array.tryFindIndex (fun n -> n = presetName) |> Option.defaultValue 0
    radios.[presetIdx].IsChecked <- Nullable<bool>(true)

    let isCustom = not (Ctx.isCustomAccelDisabled()) && Ctx.getBooleanOfName DataID.customAccelTable
    let customEnable = addCheckBox panel "Use custom table" isCustom

    let sep = System.Windows.Controls.Separator(Margin = Thickness(0.0, 8.0, 0.0, 4.0))
    panel.Children.Add(sep) |> ignore

    let thrLabel = TextBlock(Text = "Thresholds", Margin = Thickness(0.0, 4.0, 0.0, 2.0))
    panel.Children.Add(thrLabel) |> ignore
    let thrBox = TextBox(AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 60.0, TextWrapping = TextWrapping.Wrap)
    panel.Children.Add(thrBox) |> ignore

    let mulLabel = TextBlock(Text = "Multipliers", Margin = Thickness(0.0, 8.0, 0.0, 2.0))
    panel.Children.Add(mulLabel) |> ignore
    let mulBox = TextBox(AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 60.0, TextWrapping = TextWrapping.Wrap)
    panel.Children.Add(mulBox) |> ignore

    if isCustom then
        let ct = Ctx.getCustomAccelThreshold()
        let cm = Ctx.getCustomAccelMultiplier()
        if ct <> null && cm <> null then
            thrBox.Text <- ct |> Array.map string |> String.concat ","
            mulBox.Text <- cm |> Array.map (fun d -> d.ToString("F1")) |> String.concat ","
        else
            let (t, m) = formatAccelArrays presetNames.[presetIdx]
            thrBox.Text <- t
            mulBox.Text <- m
    else
        let (t, m) = formatAccelArrays presetNames.[presetIdx]
        thrBox.Text <- t
        mulBox.Text <- m

    for i in 0..4 do
        radios.[i].Checked.Add(fun _ ->
            if radios.[i].IsChecked.GetValueOrDefault() && not (customEnable.IsChecked.GetValueOrDefault()) then
                let (t, m) = formatAccelArrays presetNames.[i]
                thrBox.Text <- t
                mulBox.Text <- m
        )

    let controls = {
        Enable = accelEnable; Radios = radios; CustomEnable = customEnable
        ThrBox = thrBox; MulBox = mulBox
    }
    (panel, controls)

let private applyAccel (c: AccelControls) =
    Ctx.setBooleanOfName DataID.accelTable (c.Enable.IsChecked.GetValueOrDefault())

    let mutable presetIdx = 0
    for i in 0..4 do
        if c.Radios.[i].IsChecked.GetValueOrDefault() then presetIdx <- i
    Ctx.setAccelMultiplier presetNames.[presetIdx]

    let custom = c.CustomEnable.IsChecked.GetValueOrDefault()
    Ctx.setBooleanOfName DataID.customAccelTable custom

    if custom then
        let thrStr = c.ThrBox.Text.Trim()
        let mulStr = c.MulBox.Text.Trim()
        if thrStr = "" || mulStr = "" then
            Dialog.errorMessage ("Invalid Number") ("Error")
            false
        elif not (Ctx.setCustomAccelStrings thrStr mulStr) then
            Dialog.errorMessage ("Invalid Number") ("Error")
            false
        else true
    else true

// ========== Page: Real Wheel ==========

type private RealWheelControls = {
    Enable: CheckBox; DeltaSpin: TextBox; VSpeedSpin: TextBox; HSpeedSpin: TextBox
    QuickFirst: CheckBox; QuickTurn: CheckBox
}

let private createRealWheelPage () =
    let panel = StackPanel(Margin = Thickness(8.0))

    let rwEnable = addCheckBox panel "Enable" (Ctx.getBooleanOfName DataID.realWheelMode)
    let deltaSpin = addSpinRow panel "Wheel delta" 10 500 (Ctx.getNumberOfName DataID.wheelDelta)
    let vSpeedSpin = addSpinRow panel "Vertical speed" 10 500 (Ctx.getNumberOfName DataID.vWheelMove)
    let hSpeedSpin = addSpinRow panel "Horizontal speed" 10 500 (Ctx.getNumberOfName DataID.hWheelMove)
    let quickFirst = addCheckBox panel "Quick first scroll" (Ctx.getBooleanOfName DataID.quickFirst)
    let quickTurn = addCheckBox panel "Quick direction change" (Ctx.getBooleanOfName DataID.quickTurn)

    let controls = {
        Enable = rwEnable; DeltaSpin = deltaSpin; VSpeedSpin = vSpeedSpin; HSpeedSpin = hSpeedSpin
        QuickFirst = quickFirst; QuickTurn = quickTurn
    }
    (panel, controls)

let private applyRealWheel (c: RealWheelControls) =
    let vals = [|
        getSpinValue c.DeltaSpin 10 500 "Wheel delta"
        getSpinValue c.VSpeedSpin 10 500 "Vertical speed"
        getSpinValue c.HSpeedSpin 10 500 "Horizontal speed"
    |]
    if vals |> Array.exists Option.isNone then false
    else
        Ctx.setBooleanOfName DataID.realWheelMode (c.Enable.IsChecked.GetValueOrDefault())
        Ctx.setNumberOfName DataID.wheelDelta vals.[0].Value
        Ctx.setNumberOfName DataID.vWheelMove vals.[1].Value
        Ctx.setNumberOfName DataID.hWheelMove vals.[2].Value
        Ctx.setBooleanOfName DataID.quickFirst (c.QuickFirst.IsChecked.GetValueOrDefault())
        Ctx.setBooleanOfName DataID.quickTurn (c.QuickTurn.IsChecked.GetValueOrDefault())
        true

// ========== Page: VH Adjuster ==========

type private VhControls = {
    Enable: CheckBox; FixedRadio: RadioButton; SwitchingRadio: RadioButton
    PreferVert: CheckBox; FirstMinSpin: TextBox; SwitchSpin: TextBox
}

let private createVhAdjusterPage () =
    let panel = StackPanel(Margin = Thickness(8.0))

    let vhEnable = addCheckBox panel "Enable" (Ctx.getBooleanOfName DataID.vhAdjusterMode)

    let radioPanel = StackPanel(Orientation = Orientation.Horizontal, Margin = Thickness(0.0, 4.0, 0.0, 0.0))
    let fixedRadio = RadioButton(Content = "Fixed", Margin = Thickness(0.0, 0.0, 12.0, 0.0))
    let switchingRadio = RadioButton(Content = "Switching")
    radioPanel.Children.Add(fixedRadio) |> ignore
    radioPanel.Children.Add(switchingRadio) |> ignore
    panel.Children.Add(radioPanel) |> ignore

    if Ctx.getVhAdjusterMethodName() = "Switching" then
        switchingRadio.IsChecked <- Nullable<bool>(true)
    else
        fixedRadio.IsChecked <- Nullable<bool>(true)

    let preferVert = addCheckBox panel "Prefer vertical first" (Ctx.getBooleanOfName DataID.firstPreferVertical)
    let firstMinSpin = addSpinRow panel "Min. threshold" 1 10 (Ctx.getNumberOfName DataID.firstMinThreshold)
    let switchSpin = addSpinRow panel "Switching threshold" 10 500 (Ctx.getNumberOfName DataID.switchingThreshold)

    let controls = {
        Enable = vhEnable; FixedRadio = fixedRadio; SwitchingRadio = switchingRadio
        PreferVert = preferVert; FirstMinSpin = firstMinSpin; SwitchSpin = switchSpin
    }
    (panel, controls)

let private applyVhAdjuster (c: VhControls) =
    let vals = [|
        getSpinValue c.FirstMinSpin 1 10 "Min. threshold"
        getSpinValue c.SwitchSpin 10 500 "Switching threshold"
    |]
    if vals |> Array.exists Option.isNone then false
    else
        Ctx.setBooleanOfName DataID.vhAdjusterMode (c.Enable.IsChecked.GetValueOrDefault())
        if c.FixedRadio.IsChecked.GetValueOrDefault() then
            Ctx.setVhAdjusterMethod "Fixed"
        else
            Ctx.setVhAdjusterMethod "Switching"
        Ctx.setBooleanOfName DataID.firstPreferVertical (c.PreferVert.IsChecked.GetValueOrDefault())
        Ctx.setNumberOfName DataID.firstMinThreshold vals.[0].Value
        Ctx.setNumberOfName DataID.switchingThreshold vals.[1].Value
        true

// ========== Page: Keyboard ==========

let private HELP_KEYBOARD =
    "Character repeat delay (0\u20133): How long to hold a key\r\n" +
    "  before auto-repeat starts. 0=shortest, 3=longest.\r\n\r\n" +
    "Character repeat speed (0\u201331): Auto-repeat rate.\r\n" +
    "  31=fastest (~30 char/sec), 0=slowest (~2.5 char/sec).\r\n\r\n" +
    "Enable Filter Keys: Override keyboard repeat via the\r\n" +
    "  accessibility Filter Keys feature. When off, Windows\r\n" +
    "  reverts to standard keyboard repeat behaviour.\r\n\r\n" +
    "Lock: Reapply all keyboard settings on startup if the\r\n" +
    "  system state has changed (e.g. after reboot).\r\n\r\n" +
    "Acceptance delay: Time in ms a key must be held before\r\n" +
    "  it registers. Set to 0 to accept keys immediately.\r\n\r\n" +
    "Repeat delay: Time in ms a key must be held before\r\n" +
    "  auto-repeat begins (Filter Keys).\r\n\r\n" +
    "Repeat rate: Interval in ms between repeated keystrokes.\r\n" +
    "  Lower = faster. E.g. 16 ms \u2248 60 keys/sec.\r\n\r\n" +
    "Bounce time: Time in ms to ignore duplicate presses of\r\n" +
    "  the same key after release. Set to 0 to disable.\r\n" +
    "  Cannot be used together with the other timing fields."

type private KeyboardControls = {
    KbDelaySpin: TextBox; KbSpeedSpin: TextBox
    Enable: CheckBox; Lock: CheckBox
    AcceptSpin: TextBox; DelaySpin: TextBox; RepeatSpin: TextBox; BounceSpin: TextBox
}

let private readSystemFilterKeys () =
    let size = uint32 (System.Runtime.InteropServices.Marshal.SizeOf(typeof<WinAPI.FILTERKEYS>))
    let ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(int size)
    try
        System.Runtime.InteropServices.Marshal.WriteInt32(ptr, int size)
        WinAPI.SystemParametersInfo(WinAPI.SPI_GETFILTERKEYS, size, ptr, 0u) |> ignore
        System.Runtime.InteropServices.Marshal.PtrToStructure(ptr, typeof<WinAPI.FILTERKEYS>) :?> WinAPI.FILTERKEYS
    finally
        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr)

let private readSystemKeyboardRepeat () =
    let ptrDelay = System.Runtime.InteropServices.Marshal.AllocHGlobal(4)
    let ptrSpeed = System.Runtime.InteropServices.Marshal.AllocHGlobal(4)
    try
        WinAPI.SystemParametersInfo(WinAPI.SPI_GETKEYBOARDDELAY, 0u, ptrDelay, 0u) |> ignore
        WinAPI.SystemParametersInfo(WinAPI.SPI_GETKEYBOARDSPEED, 0u, ptrSpeed, 0u) |> ignore
        let d = System.Runtime.InteropServices.Marshal.ReadInt32(ptrDelay)
        let s = System.Runtime.InteropServices.Marshal.ReadInt32(ptrSpeed)
        (d, s)
    finally
        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptrDelay)
        System.Runtime.InteropServices.Marshal.FreeHGlobal(ptrSpeed)

let private createKeyboardPage () =
    let panel = StackPanel(Margin = Thickness(8.0))

    let (sysKbDelay, sysKbSpeed) = readSystemKeyboardRepeat()
    let kbDelaySpin = addSpinRow panel "Repeat delay" 0 3 sysKbDelay
    let kbSpeedSpin = addSpinRow panel "Repeat speed" 0 31 sysKbSpeed

    let sep1 = System.Windows.Controls.Separator(Margin = Thickness(0.0, 8.0, 0.0, 4.0))
    panel.Children.Add(sep1) |> ignore

    let fk = readSystemFilterKeys()
    let sysEnabled = (fk.dwFlags &&& WinAPI.FKF_FILTERKEYSON) <> 0u

    let enableCb = addCheckBox panel "Enable Filter Keys" sysEnabled

    let sep2 = System.Windows.Controls.Separator(Margin = Thickness(0.0, 8.0, 0.0, 4.0))
    panel.Children.Add(sep2) |> ignore

    let (fkAccept, fkDelay, fkRepeat, fkBounce) =
        if sysEnabled then
            (int fk.iWaitMSec, int fk.iDelayMSec, int fk.iRepeatMSec, int fk.iBounceMSec)
        else
            (Ctx.getNumberOfName DataID.fkAcceptanceDelay,
             Ctx.getNumberOfName DataID.fkRepeatDelay,
             Ctx.getNumberOfName DataID.fkRepeatRate,
             Ctx.getNumberOfName DataID.fkBounceTime)

    let acceptSpin = addSpinRow panel "Acceptance delay" 0 10000 fkAccept
    let delaySpin = addSpinRow panel "Repeat delay" 0 10000 fkDelay
    let repeatSpin = addSpinRow panel "Repeat rate" 0 10000 fkRepeat
    let bounceSpin = addSpinRow panel "Bounce time" 0 10000 fkBounce

    let msLabel (tb: TextBox) =
        let parent = tb.Parent :?> StackPanel
        let lbl = TextBlock(Text = "ms", VerticalAlignment = VerticalAlignment.Center, Margin = Thickness(4.0, 0.0, 0.0, 0.0))
        parent.Children.Add(lbl) |> ignore
    msLabel acceptSpin
    msLabel delaySpin
    msLabel repeatSpin
    msLabel bounceSpin

    let lockCb = addCheckBox panel "Lock (reapply on startup)" (Ctx.getBooleanOfName DataID.fkLock)

    let controls = {
        KbDelaySpin = kbDelaySpin; KbSpeedSpin = kbSpeedSpin
        Enable = enableCb; Lock = lockCb
        AcceptSpin = acceptSpin; DelaySpin = delaySpin
        RepeatSpin = repeatSpin; BounceSpin = bounceSpin
    }
    (panel, controls)

let private applyKeyboard (c: KeyboardControls) =
    let kbVals = [|
        getSpinValue c.KbDelaySpin 0 3 "Repeat delay"
        getSpinValue c.KbSpeedSpin 0 31 "Repeat speed"
    |]
    let fkVals = [|
        getSpinValue c.AcceptSpin 0 10000 "Acceptance delay"
        getSpinValue c.DelaySpin 0 10000 "Repeat delay (FK)"
        getSpinValue c.RepeatSpin 0 10000 "Repeat rate"
        getSpinValue c.BounceSpin 0 10000 "Bounce time"
    |]
    if (kbVals |> Array.exists Option.isNone) || (fkVals |> Array.exists Option.isNone) then false
    else
        Ctx.setNumberOfName DataID.kbRepeatDelay kbVals.[0].Value
        Ctx.setNumberOfName DataID.kbRepeatSpeed kbVals.[1].Value
        Ctx.applyKeyboardRepeatToSystem kbVals.[0].Value kbVals.[1].Value

        let enabled = c.Enable.IsChecked.GetValueOrDefault()
        Ctx.setBooleanOfName DataID.filterKeys enabled
        Ctx.setBooleanOfName DataID.fkLock (c.Lock.IsChecked.GetValueOrDefault())
        Ctx.setNumberOfName DataID.fkAcceptanceDelay fkVals.[0].Value
        Ctx.setNumberOfName DataID.fkRepeatDelay fkVals.[1].Value
        Ctx.setNumberOfName DataID.fkRepeatRate fkVals.[2].Value
        Ctx.setNumberOfName DataID.fkBounceTime fkVals.[3].Value

        Ctx.applyFilterKeysToSystem enabled fkVals.[0].Value fkVals.[1].Value fkVals.[2].Value fkVals.[3].Value
        true

// ========== Page: Profiles ==========

let private createProfilesPage (refreshAll: unit -> unit) =
    let panel = DockPanel(Margin = Thickness(8.0))

    let btnPanel = StackPanel(Orientation = Orientation.Horizontal, Margin = Thickness(0.0, 8.0, 0.0, 0.0))
    DockPanel.SetDock(btnPanel, Dock.Bottom)
    panel.Children.Add(btnPanel) |> ignore

    let listBox = ListBox()
    panel.Children.Add(listBox) |> ignore

    let refreshList () =
        listBox.Items.Clear()
        listBox.Items.Add("Default") |> ignore
        let sel = Ctx.getSelectedProperties()
        let mutable selIdx = 0
        let files = Properties.getPropFiles()
        for f in files do
            let name = Properties.getUserDefName f
            let idx = listBox.Items.Add(name)
            if name = sel then selIdx <- idx
        if sel = "Default" then selIdx <- 0
        if listBox.Items.Count > 0 then
            listBox.SelectedIndex <- selIdx

    refreshList()

    listBox.MouseDoubleClick.Add(fun _ ->
        let sel = listBox.SelectedIndex
        if sel >= 0 then
            let name = listBox.Items.[sel] :?> string
            Ctx.setSelectedProperties name
            Ctx.reloadProperties()
            Ctx.updateHealthTimer()
            refreshAll()
            refreshList()
    )

    let addBtn text (action: unit -> unit) =
        let btn = Button(Content = text, MinWidth = 65.0, Margin = Thickness(0.0, 0.0, 4.0, 0.0))
        btn.Click.Add(fun _ -> action())
        btnPanel.Children.Add(btn) |> ignore

    addBtn "Reload" (fun () ->
        Ctx.reloadProperties()
        Ctx.updateHealthTimer()
        refreshAll()
        refreshList()
    )
    addBtn "Save" (fun () -> Ctx.storeProperties())
    addBtn "Open Dir" (fun () -> Process.Start(Properties.CONFIG_DIR) |> ignore)
    addBtn "Add" (fun () ->
        let res = Dialog.openTextInputBox ("Properties Name") ("Add Properties")
        res |> Option.iter (fun name ->
            if not (Ctx.isValidPropertiesName name) then
                Dialog.errorMessage (sprintf "%s: %s" ("Invalid Name") name) ("Name Error")
            else
                Properties.copy (Ctx.getSelectedProperties()) name
                Ctx.setSelectedProperties name
                Ctx.reloadProperties()
                Ctx.updateHealthTimer()
                refreshAll()
                refreshList()
        )
    )
    addBtn "Delete" (fun () ->
        let sel = Ctx.getSelectedProperties()
        if sel <> "Default" then
            Properties.delete sel
            Ctx.setSelectedProperties "Default"
            Ctx.reloadProperties()
            Ctx.updateHealthTimer()
            refreshAll()
            refreshList()
    )

    panel

// ========== Reset functions ==========

let private resetGeneral (c: GeneralControls) =
    c.TriggerCombo.SelectedIndex <- 0
    c.SendMiddle.IsChecked <- Nullable<bool>(false)
    c.DraggedLock.IsChecked <- Nullable<bool>(false)
    c.KbEnable.IsChecked <- Nullable<bool>(false)
    c.VkCombo.SelectedIndex <- 0
    c.PrioCombo.SelectedIndex <- 1
    c.HealthSpin.Text <- "0"

let private resetScroll (c: ScrollControls) =
    c.CursorChange.IsChecked <- Nullable<bool>(false)
    c.HScroll.IsChecked <- Nullable<bool>(false)
    c.ReverseScroll.IsChecked <- Nullable<bool>(false)
    c.SwapScroll.IsChecked <- Nullable<bool>(false)
    c.PollSpin.Text <- "200"
    c.LockSpin.Text <- "200"
    c.VertSpin.Text <- "0"
    c.HorizSpin.Text <- "0"
    c.DragSpin.Text <- "0"

let private resetAccel (c: AccelControls) =
    c.Enable.IsChecked <- Nullable<bool>(true)
    c.Radios.[0].IsChecked <- Nullable<bool>(true)
    c.CustomEnable.IsChecked <- Nullable<bool>(false)
    let (t, m) = formatAccelArrays "M5"
    c.ThrBox.Text <- t
    c.MulBox.Text <- m

let private resetRealWheel (c: RealWheelControls) =
    c.Enable.IsChecked <- Nullable<bool>(false)
    c.DeltaSpin.Text <- "120"
    c.VSpeedSpin.Text <- "60"
    c.HSpeedSpin.Text <- "60"
    c.QuickFirst.IsChecked <- Nullable<bool>(false)
    c.QuickTurn.IsChecked <- Nullable<bool>(false)

let private resetVhAdjuster (c: VhControls) =
    c.Enable.IsChecked <- Nullable<bool>(false)
    c.FixedRadio.IsChecked <- Nullable<bool>(false)
    c.SwitchingRadio.IsChecked <- Nullable<bool>(true)
    c.PreferVert.IsChecked <- Nullable<bool>(true)
    c.FirstMinSpin.Text <- "5"
    c.SwitchSpin.Text <- "50"

let private resetKeyboard (c: KeyboardControls) =
    c.KbDelaySpin.Text <- "1"
    c.KbSpeedSpin.Text <- "31"
    c.Enable.IsChecked <- Nullable<bool>(false)
    c.Lock.IsChecked <- Nullable<bool>(false)
    c.AcceptSpin.Text <- "1000"
    c.DelaySpin.Text <- "1000"
    c.RepeatSpin.Text <- "500"
    c.BounceSpin.Text <- "0"

// ========== Main dialog ==========

let mutable private settingsOpen = false

let private showSettingsDialog () =
    if settingsOpen then () else
    settingsOpen <- true
    let window = Window()
    window.Title <- "tpkb Settings"
    window.Width <- 500.0
    window.Height <- 480.0
    window.ResizeMode <- ResizeMode.NoResize
    window.WindowStartupLocation <- WindowStartupLocation.CenterScreen

    try
        use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon-run.ico")
        use icon = new System.Drawing.Icon(stream)
        use bmp = icon.ToBitmap()
        use ms = new System.IO.MemoryStream()
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png)
        ms.Position <- 0L
        let bi = BitmapImage()
        bi.BeginInit()
        bi.StreamSource <- ms
        bi.CacheOption <- BitmapCacheOption.OnLoad
        bi.EndInit()
        window.Icon <- bi
    with _ -> ()

    let root = DockPanel()

    // Bottom button bar
    let buttonBar = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = Thickness(8.0))
    DockPanel.SetDock(buttonBar, Dock.Bottom)
    root.Children.Add(buttonBar) |> ignore

    let tabControl = TabControl(Margin = Thickness(8.0, 8.0, 8.0, 0.0))
    root.Children.Add(tabControl) |> ignore

    let mutable genControls: GeneralControls option = None
    let mutable scrControls: ScrollControls option = None
    let mutable accControls: AccelControls option = None
    let mutable rwControls: RealWheelControls option = None
    let mutable vhCtrl: VhControls option = None
    let mutable kbCtrl: KeyboardControls option = None

    let tabGeneral = TabItem(Header = "General")
    let tabScroll = TabItem(Header = "Scroll")
    let tabAccel = TabItem(Header = "Acceleration")
    let tabRealWheel = TabItem(Header = "Real Wheel")
    let tabVhAdj = TabItem(Header = "VH Adjuster")
    let tabFilterKeys = TabItem(Header = "Keyboard")
    let tabProfiles = TabItem(Header = "Profiles")
    tabControl.Items.Add(tabGeneral) |> ignore
    tabControl.Items.Add(tabScroll) |> ignore
    tabControl.Items.Add(tabAccel) |> ignore
    tabControl.Items.Add(tabRealWheel) |> ignore
    tabControl.Items.Add(tabVhAdj) |> ignore
    tabControl.Items.Add(tabFilterKeys) |> ignore
    tabControl.Items.Add(tabProfiles) |> ignore

    let btnApply = Button(Content = "Apply", MinWidth = 75.0, IsEnabled = false)
    let mutable initializing = false
    let markChanged () =
        if not initializing then btnApply.IsEnabled <- true

    let addTabButtons (tab: TabItem) (content: UIElement) helpText (resetAction: unit -> unit) =
        let outer = DockPanel()
        let helpBar = StackPanel(Orientation = Orientation.Horizontal, Margin = Thickness(8.0, 4.0, 8.0, 4.0))
        DockPanel.SetDock(helpBar, Dock.Bottom)
        let helpBtn = Button(Content = "?", MinWidth = 28.0, Margin = Thickness(0.0, 0.0, 4.0, 0.0))
        helpBtn.Click.Add(fun _ -> showHelp helpText)
        helpBar.Children.Add(helpBtn) |> ignore
        let resetBtn = Button(Content = "Reset", MinWidth = 50.0)
        resetBtn.Click.Add(fun _ ->
            initializing <- true
            resetAction()
            initializing <- false
            markChanged()
        )
        helpBar.Children.Add(resetBtn) |> ignore
        outer.Children.Add(helpBar) |> ignore
        let scroll = ScrollViewer(VerticalScrollBarVisibility = ScrollBarVisibility.Auto)
        scroll.Content <- content
        outer.Children.Add(scroll) |> ignore
        tab.Content <- outer

    let rec hookChangeEvents (element: UIElement) =
        match element with
        | :? CheckBox as cb ->
            cb.Checked.Add(fun _ -> markChanged())
            cb.Unchecked.Add(fun _ -> markChanged())
        | :? RadioButton as rb ->
            rb.Checked.Add(fun _ -> markChanged())
        | :? ComboBox as combo ->
            combo.SelectionChanged.Add(fun _ -> markChanged())
        | :? TextBox as tb ->
            tb.TextChanged.Add(fun _ -> markChanged())
        | :? Panel as panel ->
            for i in 0 .. panel.Children.Count - 1 do
                hookChangeEvents (panel.Children.[i])
        | :? Decorator as dec when dec.Child <> null ->
            hookChangeEvents dec.Child
        | :? ContentControl as cc ->
            match cc.Content with
            | :? UIElement as child -> hookChangeEvents child
            | _ -> ()
        | _ -> ()

    let rec refreshAll () =
        initializing <- true

        let (gp, gc) = createGeneralPage()
        genControls <- Some gc
        addTabButtons tabGeneral gp HELP_GENERAL (fun () -> resetGeneral gc)
        hookChangeEvents (tabGeneral.Content :?> UIElement)

        let (sp, sc) = createScrollPage()
        scrControls <- Some sc
        addTabButtons tabScroll sp HELP_SCROLL (fun () -> resetScroll sc)
        hookChangeEvents (tabScroll.Content :?> UIElement)

        let (ap, ac) = createAccelPage()
        accControls <- Some ac
        addTabButtons tabAccel ap HELP_ACCEL (fun () -> resetAccel ac)
        hookChangeEvents (tabAccel.Content :?> UIElement)

        let (rp, rc) = createRealWheelPage()
        rwControls <- Some rc
        addTabButtons tabRealWheel rp HELP_REALWHEEL (fun () -> resetRealWheel rc)
        hookChangeEvents (tabRealWheel.Content :?> UIElement)

        let (vp, vc) = createVhAdjusterPage()
        vhCtrl <- Some vc
        addTabButtons tabVhAdj vp HELP_VHADJ (fun () -> resetVhAdjuster vc)
        hookChangeEvents (tabVhAdj.Content :?> UIElement)

        let (kbp, kbc) = createKeyboardPage()
        kbCtrl <- Some kbc
        addTabButtons tabFilterKeys kbp HELP_KEYBOARD (fun () -> resetKeyboard kbc)
        hookChangeEvents (tabFilterKeys.Content :?> UIElement)

        let pp = createProfilesPage refreshAll
        tabProfiles.Content <- pp

        initializing <- false
        btnApply.IsEnabled <- false

    refreshAll()

    let doApply () =
        match genControls, scrControls, accControls, rwControls, vhCtrl, kbCtrl with
        | Some gc, Some sc, Some ac, Some rc, Some vc, Some kc ->
            if applyGeneral gc && applyScroll sc && applyAccel ac && applyRealWheel rc && applyVhAdjuster vc && applyKeyboard kc then
                true
            else false
        | _ -> true

    let btnOk = Button(Content = "OK", MinWidth = 75.0, Margin = Thickness(0.0, 0.0, 4.0, 0.0))
    btnOk.Click.Add(fun _ ->
        if doApply() then
            Ctx.storeProperties()
            window.DialogResult <- Nullable<bool>(true)
    )
    buttonBar.Children.Add(btnOk) |> ignore

    let btnCancel = Button(Content = "Cancel", MinWidth = 75.0, Margin = Thickness(0.0, 0.0, 4.0, 0.0))
    btnCancel.Click.Add(fun _ ->
        window.DialogResult <- Nullable<bool>(false)
    )
    buttonBar.Children.Add(btnCancel) |> ignore

    btnApply.Click.Add(fun _ ->
        if doApply() then
            Ctx.storeProperties()
            btnApply.IsEnabled <- false
    )
    buttonBar.Children.Add(btnApply) |> ignore

    window.Content <- root
    try window.ShowDialog() |> ignore
    finally settingsOpen <- false

let setShowSettings () =
    Ctx.setShowSettings showSettingsDialog
