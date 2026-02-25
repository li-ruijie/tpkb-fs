/// Localization utilities for detecting system language and translating UI strings.
///
/// Determines the current UI language from system culture and provides translation
/// lookup to LocaleData for Japanese localization.
///
/// Language detection uses CultureInfo.CurrentUICulture to detect the system's
/// UI language preference and defaults to English for all non-Japanese systems.
module Locale

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

open System.Diagnostics
open System.Globalization

/// Detects the system's UI language.
/// Returns "ja" for Japanese systems, "en" for everything else.
let getLanguage () =
    match CultureInfo.CurrentUICulture.TwoLetterISOLanguageName with
    | "ja" -> "ja" // Japanese
    | _ -> "en" // Other

/// Translates a message to the specified language.
/// For English, returns the original message.
/// For Japanese, looks up translation in LocaleData.
let convLang lang msg =
    match lang with
    | DataID.English -> msg
    | DataID.Japanese -> LocaleData.get_ja msg
    | _ ->
        Debug.WriteLine ("Not supported language: " + lang)
        msg