/// UI dialog utilities for displaying error messages and input boxes.
///
/// Provides functions for showing message boxes and collecting user input
/// for numbers and text values. Uses Windows Forms MessageBox for errors
/// and Microsoft.VisualBasic.Interaction.InputBox for user input.
module Dialog

(*
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the GNU General Public License v3.0.
 *)

open System
open System.Windows.Forms
open Microsoft.VisualBasic

/// Displays an error message box with the specified message and title.
let errorMessage msg title =
    MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error) |> ignore

/// Displays an error message box for an exception, using the exception type as the title.
let errorMessageE (e: Exception) =
    errorMessage e.Message (e.GetType().Name)

/// Opens an input box for entering text.
/// Returns Some(text) if non-empty input, None if cancelled or empty.
let openTextInputBox msg title: string option =
    let input = Interaction.InputBox(msg, title)
    if input <> "" then Some(input) else None

