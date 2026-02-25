/// Process priority management for the application.
///
/// Allows setting the process priority class (Normal, AboveNormal, High)
/// to ensure responsive mouse hook processing. Higher priority reduces
/// the chance of scroll events being delayed by other applications.
///
/// Default is AboveNormal which provides good responsiveness without
/// potentially starving other processes.
module ProcessPriority

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System
open System.Diagnostics

type Priority =
    | Normal
    | AboveNormal
    | High

    member self.Name = Mouse.getUnionCaseName(self)

let getPriority = function
    | DataID.High -> High
    | DataID.AboveNormal | "Above Normal" -> AboveNormal
    | DataID.Normal -> Normal
    | e -> raise (ArgumentException(e))

let setPriority p =
    let cp = Process.GetCurrentProcess()
    match p with
    | Normal -> cp.PriorityClass <- ProcessPriorityClass.Normal
    | AboveNormal -> cp.PriorityClass <- ProcessPriorityClass.AboveNormal
    | High -> cp.PriorityClass <- ProcessPriorityClass.High

