/// UI dialog utilities for displaying error messages and input boxes.
///
/// Provides functions for showing message boxes and collecting user input
/// for numbers and text values. Uses Windows Forms MessageBox for errors
/// and Microsoft.VisualBasic.Interaction.InputBox for user input.
module Dialog

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
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

/// Validates that input is a number within the specified range.
let private isValidNumber input low up =
    match Int32.TryParse(input)  with
    | (true, res) -> res >= low && res <= up
    | _ -> false

/// Opens an input box for entering a number within a range.
/// Returns Ok(number) if valid input, Error(input) if invalid or cancelled.
/// The prompt shows the parameter name and valid range (e.g., "pollTimeout (150 - 500)").
let openNumberInputBox name title low up cur =
    let msg = sprintf "%s (%d - %d)" name low up
    let dvalue = cur.ToString()
    let input = Interaction.InputBox(msg, title, dvalue)
    if isValidNumber input low up then
        Ok (Int32.Parse(input))
    else
        Error (input)

/// Opens an input box for entering text.
/// Returns Some(text) if non-empty input, None if cancelled or empty.
let openTextInputBox msg title: string option =
    let input = Interaction.InputBox(msg, title)
    if input <> "" then Some(input) else None

