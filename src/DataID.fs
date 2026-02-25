/// String constants for property names, trigger types, keyboard keys, and other identifiers.
/// These constants are used as keys in the properties file and for type-safe identification
/// of settings, triggers, acceleration multipliers, and UI elements.
/// All values are compile-time literals for use in pattern matching.
module DataID

(*
 * Copyright (c) 2016-2021 Yuki Ono
 * Copyright (c) 2026 Li Ruijie
 * Licensed under the MIT License.
 *)

// ============================================================================
// Numeric Settings - Integer values configurable via Set Number menu
// ============================================================================

/// Time in ms to wait for second button press in LR trigger mode (150-500)
[<Literal>]
let pollTimeout = "pollTimeout"

/// Time in ms before scroll mode locks and ignores trigger release (150-500)
[<Literal>]
let scrollLocktime = "scrollLocktime"

/// Minimum vertical movement in pixels before scroll triggers (0-500)
[<Literal>]
let verticalThreshold = "verticalThreshold"

/// Minimum horizontal movement in pixels before scroll triggers (0-500)
[<Literal>]
let horizontalThreshold = "horizontalThreshold"

/// Wheel delta value for real wheel mode, standard is 120 (10-500)
[<Literal>]
let wheelDelta = "wheelDelta"

/// Vertical movement in pixels per wheel tick in real wheel mode (10-500)
[<Literal>]
let vWheelMove = "vWheelMove"

/// Horizontal movement in pixels per wheel tick in real wheel mode (10-500)
[<Literal>]
let hWheelMove = "hWheelMove"

/// Minimum movement to determine initial V/H direction in VH adjuster (1-10)
[<Literal>]
let firstMinThreshold = "firstMinThreshold"

/// Movement threshold to switch between V/H in switching mode (10-500)
[<Literal>]
let switchingThreshold = "switchingThreshold"

/// Minimum drag movement before drag triggers activate (0-500)
[<Literal>]
let dragThreshold = "dragThreshold"

// ============================================================================
// Boolean Settings - On/Off toggles
// ============================================================================

/// Use real wheel mode (discrete wheel ticks) vs direct mode (pixel-based)
[<Literal>]
let realWheelMode = "realWheelMode"

/// Change cursor icon to indicate scroll direction
[<Literal>]
let cursorChange = "cursorChange"

/// Enable horizontal scrolling (left-right mouse movement)
[<Literal>]
let horizontalScroll = "horizontalScroll"

/// Reverse/flip scroll direction
[<Literal>]
let reverseScroll = "reverseScroll"

/// Make first wheel tick respond faster in real wheel mode
[<Literal>]
let quickFirst = "quickFirst"

/// Make direction changes respond faster in real wheel mode
[<Literal>]
let quickTurn = "quickTurn"

/// Enable acceleration table for scroll speed
[<Literal>]
let accelTable = "accelTable"

/// Use custom acceleration table instead of built-in M5-M9
[<Literal>]
let customAccelTable = "customAccelTable"

/// Lock scroll mode after drag trigger release
[<Literal>]
let draggedLock = "draggedLock"

/// Swap vertical and horizontal scroll axes
[<Literal>]
let swapScroll = "swapScroll"

/// Send middle click when single trigger released without scrolling
[<Literal>]
let sendMiddleClick = "sendMiddleClick"

/// Enable keyboard hook for keyboard-based triggers
[<Literal>]
let keyboardHook = "keyboardHook"

/// Enable VH adjuster to auto-detect scroll direction
[<Literal>]
let vhAdjusterMode = "vhAdjusterMode"

/// Prefer vertical scroll when movement is ambiguous
[<Literal>]
let firstPreferVertical = "firstPreferVertical"

/// Pass mode - disable all scroll functionality temporarily
[<Literal>]
let passMode = "passMode"

// ============================================================================
// String Settings - Named values stored as strings
// ============================================================================

/// Current trigger type (LR, Left, Right, Middle, etc.)
[<Literal>]
let firstTrigger = "firstTrigger"

/// Acceleration multiplier preset (M5-M9)
[<Literal>]
let accelMultiplier = "accelMultiplier"

/// Process priority level (High, AboveNormal, Normal)
[<Literal>]
let processPriority = "processPriority"

/// Virtual key code for keyboard trigger
[<Literal>]
let targetVKCode = "targetVKCode"

/// VH adjuster method (Fixed or Switching)
[<Literal>]
let vhAdjusterMethod = "vhAdjusterMethod"

/// UI language code (en, ja)
[<Literal>]
let uiLanguage = "uiLanguage"

// ============================================================================
// Trigger Types - Mouse button combinations to activate scroll mode
// ============================================================================

/// Left+Right simultaneous press (either order)
[<Literal>]
let LR = "LR"

/// Left button triggers, Right exits
[<Literal>]
let Left = "Left"

/// Right button triggers, Left exits
[<Literal>]
let Right = "Right"

/// Middle button (wheel click) single trigger
[<Literal>]
let Middle = "Middle"

/// X1 (back) button single trigger
[<Literal>]
let X1 = "X1"

/// X2 (forward) button single trigger
[<Literal>]
let X2 = "X2"

/// Left button drag trigger
[<Literal>]
let LeftDrag = "LeftDrag"

/// Right button drag trigger
[<Literal>]
let RightDrag = "RightDrag"

/// Middle button drag trigger
[<Literal>]
let MiddleDrag = "MiddleDrag"

/// X1 button drag trigger
[<Literal>]
let X1Drag = "X1Drag"

/// X2 button drag trigger
[<Literal>]
let X2Drag = "X2Drag"

/// No trigger - scroll disabled
[<Literal>]
let None = "None"

// ============================================================================
// Virtual Key Codes - Keyboard keys for keyboard trigger mode
// ============================================================================

[<Literal>]
let VK_TAB = "VK_TAB"
[<Literal>]
let VK_PAUSE = "VK_PAUSE"
[<Literal>]
let VK_CAPITAL = "VK_CAPITAL"
[<Literal>]
let VK_CONVERT = "VK_CONVERT"
[<Literal>]
let VK_NONCONVERT = "VK_NONCONVERT"
[<Literal>]
let VK_PRIOR = "VK_PRIOR"
[<Literal>]
let VK_NEXT = "VK_NEXT"
[<Literal>]
let VK_END = "VK_END"
[<Literal>]
let VK_HOME = "VK_HOME"
[<Literal>]
let VK_SNAPSHOT = "VK_SNAPSHOT"
[<Literal>]
let VK_INSERT = "VK_INSERT"
[<Literal>]
let VK_DELETE = "VK_DELETE"
[<Literal>]
let VK_LWIN = "VK_LWIN"
[<Literal>]
let VK_RWIN = "VK_RWIN"
[<Literal>]
let VK_APPS = "VK_APPS"
[<Literal>]
let VK_NUMLOCK = "VK_NUMLOCK"
[<Literal>]
let VK_SCROLL = "VK_SCROLL"
[<Literal>]
let VK_LSHIFT = "VK_LSHIFT"
[<Literal>]
let VK_RSHIFT = "VK_RSHIFT"
[<Literal>]
let VK_LCONTROL = "VK_LCONTROL"
[<Literal>]
let VK_RCONTROL = "VK_RCONTROL"
[<Literal>]
let VK_LMENU = "VK_LMENU"
[<Literal>]
let VK_RMENU = "VK_RMENU"

// ============================================================================
// Custom Acceleration - Arrays defined in properties file only
// ============================================================================

/// Custom threshold array for acceleration (comma-separated integers)
[<Literal>]
let customAccelThreshold = "customAccelThreshold"

/// Custom multiplier array for acceleration (comma-separated doubles)
[<Literal>]
let customAccelMultiplier = "customAccelMultiplier"

// ============================================================================
// Process Priority Levels
// ============================================================================

[<Literal>]
let High = "High"
[<Literal>]
let AboveNormal = "AboveNormal"
[<Literal>]
let Normal = "Normal"

// ============================================================================
// VH Adjuster Methods
// ============================================================================

/// Fixed - direction determined once and stays fixed
[<Literal>]
let Fixed = "Fixed"

/// Switching - direction can change based on movement
[<Literal>]
let Switching = "Switching"

// ============================================================================
// Acceleration Multiplier Presets (from Kensington MouseWorks)
// ============================================================================

/// Multiplier preset: 1.0 to 4.8
[<Literal>]
let M5 = "M5"

/// Multiplier preset: 1.2 to 5.8
[<Literal>]
let M6 = "M6"

/// Multiplier preset: 1.4 to 6.7
[<Literal>]
let M7 = "M7"

/// Multiplier preset: 1.6 to 7.7
[<Literal>]
let M8 = "M8"

/// Multiplier preset: 1.8 to 8.7
[<Literal>]
let M9 = "M9"

// ============================================================================
// UI Language Codes
// ============================================================================

/// English language
[<Literal>]
let English = "en"

/// Japanese language
[<Literal>]
let Japanese = "ja"
