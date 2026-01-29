/// Inter-process messaging for controlling a running tpkb instance.
///
/// Uses a Windows Named Pipe (\\.\pipe\tpkb) for IPC. The running instance
/// hosts a pipe server on a background thread; command-line senders connect as
/// clients and write a 4-byte message.
///
/// Default pipe security restricts access to the same user account.
module W10Message

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Diagnostics
open System.IO.Pipes
open System.Security.AccessControl
open System.Security.Principal
open System.Threading
open System.Windows.Forms

let private PIPE_NAME =
    let sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId
    sprintf "LOCAL\\tpkb_%d" sessionId

let private W10_MESSAGE_BASE = 264816059 &&& 0x0FFFFFFF
let W10_MESSAGE_EXIT = W10_MESSAGE_BASE + 1
let W10_MESSAGE_PASSMODE = W10_MESSAGE_BASE + 2
let W10_MESSAGE_RELOAD_PROP = W10_MESSAGE_BASE + 3
let W10_MESSAGE_INIT_STATE = W10_MESSAGE_BASE + 4

let private setBoolBit msg b =
    msg ||| if b then 0x10000000 else 0x00000000

let private getBoolBit msg =
    (msg &&& 0xF0000000) <> 0

let private getFlag msg =
    msg &&& 0x0FFFFFFF

(* ========== Message dispatch ========== *)

let private dispatchMessage (msg: int) =
    match getFlag msg with
    | n when n = W10_MESSAGE_EXIT ->
        Debug.WriteLine("recv W10_MESSAGE_EXIT")
        Ctx.exitAction()
    | n when n = W10_MESSAGE_PASSMODE ->
        Debug.WriteLine("recv W10_MESSAGE_PASSMODE")
        Ctx.setPassMode (getBoolBit msg)
    | n when n = W10_MESSAGE_RELOAD_PROP ->
        Debug.WriteLine("recv W10_MESSAGE_RELOAD_PROP")
        Ctx.reloadProperties()
    | n when n = W10_MESSAGE_INIT_STATE ->
        Debug.WriteLine("recv W10_MESSAGE_INIT_STATE")
        Ctx.initState()
    | _ -> ()

(* ========== Server (running instance) ========== *)

/// Hidden window for marshalling IPC messages to the UI thread.
type private IpcWindow() =
    inherit NativeWindow()
    static let WM_IPC = int WinAPI.Message.WM_APP + 1
    do
        let cp = CreateParams()
        cp.Parent <- WinAPI.RawInput.HWND_MESSAGE
        base.CreateHandle(cp)
    override self.WndProc(m: Message byref) =
        if m.Msg = WM_IPC then
            dispatchMessage (int m.WParam)
        else
            base.WndProc(&m)
    member self.PostIpc(msg: int) =
        WinAPI.PostMessageW(self.Handle, uint32 WM_IPC, nativeint msg, nativeint 0) |> ignore

let mutable private ipcWindow: IpcWindow option = None
let mutable private serverThread: Thread option = None
let mutable private serverRunning = false

/// Creates a PipeSecurity that grants access only to the current user,
/// with a Medium Mandatory Label for cross-elevation IPC.
let private createPipeSecurity () =
    let ps = PipeSecurity()
    let user = WindowsIdentity.GetCurrent().User
    ps.AddAccessRule(PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow))
    try
        let sddl = ps.GetSecurityDescriptorSddlForm(AccessControlSections.Access)
        ps.SetSecurityDescriptorSddlForm(sddl + "S:(ML;;NW;;;ME)", AccessControlSections.Access ||| AccessControlSections.Audit)
    with _ -> ()
    ps

let private pipeSecurity = createPipeSecurity()

let private serverProc () =
    while serverRunning do
        try
            use server = new NamedPipeServerStream(PIPE_NAME, PipeDirection.In, 1,
                              PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4, 0, pipeSecurity)
            server.WaitForConnection()
            let buf = Array.zeroCreate<byte> 4
            use cts = new System.Threading.CancellationTokenSource(250)
            let n =
                try
                    server.ReadAsync(buf, 0, 4, cts.Token).Result
                with
                | :? System.AggregateException -> 0
            if n = 4 then
                let msg = BitConverter.ToInt32(buf, 0)
                match ipcWindow with
                | Some w -> w.PostIpc(msg)
                | None -> ()
        with
        | :? System.IO.IOException -> () // Pipe broken or closed
        | e ->
            Debug.WriteLine(sprintf "IPC server error: %s" e.Message)
            Thread.Sleep(100)

let serverStart () =
    let w = IpcWindow()
    ipcWindow <- Some w
    serverRunning <- true
    let t = Thread(serverProc)
    t.IsBackground <- true
    t.Start()
    serverThread <- Some t

let serverStop () =
    serverRunning <- false
    // Connect to unblock WaitForConnection
    try
        use client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out)
        client.Connect(100)
    with _ -> ()
    match ipcWindow with
    | Some w -> w.DestroyHandle(); ipcWindow <- None
    | None -> ()

(* ========== Client (--send* commands) ========== *)

let private sendMessage (msg: int) =
    try
        use client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out)
        client.Connect(2000)
        let buf = BitConverter.GetBytes(msg)
        client.Write(buf, 0, 4)
    with
    | e -> Debug.WriteLine(sprintf "IPC send error: %s" e.Message)

let sendExit () =
    Debug.WriteLine("send W10_MESSAGE_EXIT")
    sendMessage W10_MESSAGE_EXIT

let sendPassMode b =
    Debug.WriteLine("send W10_MESSAGE_PASSMODE")
    let msg = setBoolBit W10_MESSAGE_PASSMODE b
    sendMessage msg

let sendReloadProp () =
    Debug.WriteLine("send W10_MESSAGE_RELOAD_PROP")
    sendMessage W10_MESSAGE_RELOAD_PROP

let sendInitState () =
    Debug.WriteLine("send W10_MESSAGE_INIT_STATE")
    sendMessage W10_MESSAGE_INIT_STATE
