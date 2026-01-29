/// Application entry point and initialization.
///
/// Handles command-line argument processing, double-launch prevention,
/// admin privilege checking, and main message loop setup.
///
/// Startup sequence:
/// 1. Process command-line arguments (--send* commands or profile name)
/// 2. Load properties file
/// 3. Check for double launch (via file lock)
/// 4. Show admin privilege warning if not elevated
/// 5. Initialize function callbacks between modules
/// 6. Load full properties and apply settings
/// 7. Create system tray icon
/// 8. Install mouse hook
/// 9. Enter Windows message loop
///
/// Shutdown (procExit):
/// 1. Unhook mouse/keyboard hooks
/// 2. Save properties to file
/// 3. Release file lock
(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Diagnostics
open System.Threading
open System.Windows.Forms
open Microsoft.Win32
open System.Security.Principal

let private ADMIN_MESSAGE =
    "tpkb is not running as administrator. " +
    "It may not work in some windows. " +
    "Running as administrator is recommended."

let private isAdmin () =
    let myDomain = Thread.GetDomain();
    myDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);
    let myPrincipal = Thread.CurrentPrincipal :?> WindowsPrincipal
    myPrincipal.IsInRole(WindowsBuiltInRole.Administrator)

let private relaunchAsAdmin () =
    try
        let psi = ProcessStartInfo(Application.ExecutablePath, UseShellExecute = true, Verb = "runas")
        Process.Start(psi) |> ignore
        true
    with _ -> false

/// Check if any other tpkb process is running elevated.
/// Matches by absolute executable path to avoid name collisions.
let private isOtherInstanceElevated () =
    let current = Process.GetCurrentProcess()
    let myPath = Application.ExecutablePath
    let mutable elevated = false
    try
        let snap = WinAPI.CreateToolhelp32Snapshot(0x2u, 0u)
        if snap <> nativeint -1 then
            try
                let mutable pe = WinAPI.PROCESSENTRY32W(dwSize = uint32 (System.Runtime.InteropServices.Marshal.SizeOf(typeof<WinAPI.PROCESSENTRY32W>)))
                let mutable ok = WinAPI.Process32FirstW(snap, &pe)
                while ok && not elevated do
                    if pe.th32ProcessID <> uint32 current.Id then
                        let hProc = WinAPI.OpenProcess(0x1000u, false, pe.th32ProcessID)
                        if hProc <> nativeint 0 then
                            try
                                let buf = System.Text.StringBuilder(260)
                                let mutable sz = 260
                                let pathMatch =
                                    WinAPI.QueryFullProcessImageName(hProc, 0, buf, &sz) &&
                                    System.String.Equals(buf.ToString(), myPath, System.StringComparison.OrdinalIgnoreCase)
                                if pathMatch then
                                    let mutable hToken = nativeint 0
                                    if WinAPI.OpenProcessToken(hProc, 0x8u, &hToken) then
                                        try
                                            let ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4)
                                            try
                                                let mutable retLen = 0u
                                                if WinAPI.GetTokenInformation(hToken, 20, ptr, 4u, &retLen) then
                                                    if System.Runtime.InteropServices.Marshal.ReadInt32(ptr) <> 0 then
                                                        elevated <- true
                                            finally
                                                System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr)
                                        finally
                                            WinAPI.CloseHandle(hToken) |> ignore
                            finally
                                WinAPI.CloseHandle(hProc) |> ignore
                    ok <- WinAPI.Process32NextW(snap, &pe)
            finally
                WinAPI.CloseHandle(snap) |> ignore
    with _ -> ()
    elevated

let private checkDoubleLaunch () =
    if PreventMultiInstance.tryLock() then () else

    let result =
        System.Windows.MessageBox.Show(
            "tpkb is already running. Restart?",
            "tpkb",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question)
    if result <> System.Windows.MessageBoxResult.Yes then
        Environment.Exit(0)

    let wasAdmin = isOtherInstanceElevated()
    W10Message.sendExit()

    let mutable acquired = false
    for _ in 1..20 do
        if not acquired then
            Thread.Sleep(250)
            acquired <- PreventMultiInstance.tryLock()

    if not acquired then
        Dialog.errorMessage "Error" "Error"
        Environment.Exit(1)

    if wasAdmin && not (isAdmin()) then
        PreventMultiInstance.unlock()
        relaunchAsAdmin() |> ignore
        Environment.Exit(0)

/// Shows admin warning dialog with 3 buttons (WPF). Returns 1=RunAsAdmin, 2=Continue, 0=Exit.
let private showAdminDialog () =
    let window = System.Windows.Window()
    window.Title <- "tpkb"
    window.Width <- 480.0
    window.Height <- 190.0
    window.ResizeMode <- System.Windows.ResizeMode.NoResize
    window.WindowStartupLocation <- System.Windows.WindowStartupLocation.CenterScreen

    let root = System.Windows.Controls.DockPanel(Margin = System.Windows.Thickness(12.0))

    let buttonBar = System.Windows.Controls.StackPanel(
                        Orientation = System.Windows.Controls.Orientation.Horizontal,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Margin = System.Windows.Thickness(0.0, 8.0, 0.0, 0.0))
    System.Windows.Controls.DockPanel.SetDock(buttonBar, System.Windows.Controls.Dock.Bottom)
    root.Children.Add(buttonBar) |> ignore

    let msgPanel = System.Windows.Controls.StackPanel(Orientation = System.Windows.Controls.Orientation.Horizontal)
    root.Children.Add(msgPanel) |> ignore

    let warnIcon = System.Windows.Controls.Image(
                       Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                                    System.Drawing.SystemIcons.Warning.Handle,
                                    System.Windows.Int32Rect.Empty,
                                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()),
                       Width = 32.0, Height = 32.0,
                       Margin = System.Windows.Thickness(0.0, 0.0, 12.0, 0.0),
                       VerticalAlignment = System.Windows.VerticalAlignment.Top)
    msgPanel.Children.Add(warnIcon) |> ignore

    let lbl = System.Windows.Controls.TextBlock(
                  Text = ADMIN_MESSAGE,
                  TextWrapping = System.Windows.TextWrapping.Wrap,
                  MaxWidth = 380.0,
                  VerticalAlignment = System.Windows.VerticalAlignment.Center)
    msgPanel.Children.Add(lbl) |> ignore

    let mutable result = 0

    // Shield icon for "Run as Admin" button
    let shieldSource =
        try
            System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                System.Drawing.SystemIcons.Shield.Handle,
                System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions())
        with _ -> null

    let btnAdminContent = System.Windows.Controls.StackPanel(Orientation = System.Windows.Controls.Orientation.Horizontal)
    if shieldSource <> null then
        let shieldImg = System.Windows.Controls.Image(Source = shieldSource, Width = 16.0, Height = 16.0,
                            Margin = System.Windows.Thickness(0.0, 0.0, 4.0, 0.0))
        btnAdminContent.Children.Add(shieldImg) |> ignore
    btnAdminContent.Children.Add(System.Windows.Controls.TextBlock(Text = "Run as Admin")) |> ignore

    let btnAdmin = System.Windows.Controls.Button(Content = btnAdminContent, MinWidth = 130.0,
                       Margin = System.Windows.Thickness(0.0, 0.0, 4.0, 0.0), Padding = System.Windows.Thickness(8.0, 4.0, 8.0, 4.0))
    btnAdmin.Click.Add(fun _ -> result <- 1; window.Close())
    buttonBar.Children.Add(btnAdmin) |> ignore

    let btnContinue = System.Windows.Controls.Button(Content = "Continue", MinWidth = 80.0,
                          Margin = System.Windows.Thickness(0.0, 0.0, 4.0, 0.0), Padding = System.Windows.Thickness(8.0, 4.0, 8.0, 4.0))
    btnContinue.Click.Add(fun _ -> result <- 2; window.Close())
    buttonBar.Children.Add(btnContinue) |> ignore

    let btnExit = System.Windows.Controls.Button(Content = "Exit", MinWidth = 80.0,
                      Padding = System.Windows.Thickness(8.0, 4.0, 8.0, 4.0))
    btnExit.Click.Add(fun _ -> result <- 0; window.Close())
    buttonBar.Children.Add(btnExit) |> ignore

    window.Content <- root
    window.ShowDialog() |> ignore
    result

let private checkAdmin () =
    if not (isAdmin ()) then
        match showAdminDialog() with
        | 1 ->
            PreventMultiInstance.unlock()
            if relaunchAsAdmin() then
                Environment.Exit(0)
        | 2 -> ()
        | _ -> Environment.Exit(0)

let private procExit () =
    Debug.WriteLine("procExit")

    W10Message.serverStop()
    WinHook.unhook()
    Ctx.storeProperties()
    PreventMultiInstance.unlock()

let private getBool (sl: string list): bool =
    try
        match sl with
        | s :: _ -> Boolean.Parse(s)
        | _ -> true
    with
        | :? FormatException as e ->
            Dialog.errorMessageE e
            Environment.Exit(1)
            false

let private setSelectedProperties name =
    if Properties.exists(name) then
        Ctx.setSelectedProperties name
    else
        Dialog.errorMessage (sprintf "%s: %s" "Properties does not exist" name) "Error"

let private unknownCommand name =
    Dialog.errorMessage (sprintf "%s: %s" "Unknown Command" name) "Command Error"
    Environment.Exit(1)

let private procArgv (argv: string[]) =
    Debug.WriteLine("procArgv")

    match argv |> Array.toList with
    | "--sendExit" :: _ -> W10Message.sendExit ()
    | "--sendPassMode" :: rest -> W10Message.sendPassMode (getBool(rest))
    | "--sendReloadProp" :: _ -> W10Message.sendReloadProp ()
    | "--sendInitState" :: _ -> W10Message.sendInitState ()
    | name :: _ when name.StartsWith("--") -> unknownCommand name
    | name :: _ -> setSelectedProperties name
    | _ -> ()

    if argv.Length > 0 && argv.[0].StartsWith("--send") then
        Environment.Exit(0)


let private initSetFunctions () =
    Dispatcher.setMouseDispatcher ()
    Dispatcher.setKeyboardDispatcher ()
    EventHandler.setChangeTrigger ()
    Windows.setSendWheelRaw ()
    Windows.setInitScroll ()
    EventWaiter.setOfferEW ()
    EventHandler.setInitStateMEH ()
    KEventHandler.setInitStateKEH ()
    Settings.setShowSettings ()

[<STAThread>]
[<EntryPoint>]
let main argv =
    Application.EnableVisualStyles()
    Application.SetCompatibleTextRenderingDefault(false)
    procArgv argv

    Ctx.loadPropertiesFileOnly ()
    checkDoubleLaunch ()
    checkAdmin ()

    SystemEvents.SessionEnding.Add (fun _ ->
        try
            procExit()
        with
        | _ -> () // Silently ignore errors during shutdown to prevent crash
    )
    initSetFunctions ()

    Ctx.loadProperties false
    Ctx.applyFilterKeys()
    Ctx.setSystemTray ()
    W10Message.serverStart()
    if not (WinHook.setMouseHook ()) then
        Dialog.errorMessage (sprintf "%s: %s" "Failed mouse hook install" (WinError.getLastErrorMessage())) "Error"
        Environment.Exit(1)
    Ctx.startHealthTimer()

    Application.Run()
    Debug.WriteLine("Exit message loop")
    procExit()
    0
