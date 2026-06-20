# CtrlHanabi

[日本語はこちら](./README.ja.md)

CtrlHanabi is a Windows resident app that launches fireworks when you tap the `Ctrl` key repeatedly.  
It renders a transparent WPF overlay on the desktop and displays the full effect from rocket ascent to burst.

![CtrlHanabi demo](./CtrlHanabi_v1.0.4.gif)

## Features

- Double-tap `Ctrl` to launch a regular firework
- Triple-tap `Ctrl` to launch a starmine sequence
- Fireworks follow the current mouse cursor position
- Runs from the task tray
- Toggle auto-start with Windows
- Save and load settings from a config file
- Choose which display is used for starmine playback
  The display index is configurable in the settings file and starts at `1`

## Firework Effects

- Mortar tube rendering
- Launch animation
- Spherical 3D-style particle spread
- Afterglow and glow effects
- Randomized firework colors

The current implementation randomly generates these burst types:

- Chrysanthemum
- Botan
- Kamurogiku

The launch path can also include these variations:

- Standard ascent
- Silver-dragon style tail
- Mid-flight small-flower bloom

## Usage

1. Start the app.
2. After it is running in the background, press `Ctrl` twice quickly anywhere to launch a regular firework.
3. Press `Ctrl` three times quickly to launch a starmine sequence.

To exit:

- Select `Exit` from the task tray icon menu
- Press `Ctrl` five times quickly to open the exit confirmation dialog

## Task Tray Menu

- `Run at Windows startup`
  Uses the registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- `Launch starmine every hour`
  When enabled, a starmine launches from the center of the screen at `59:30` every hour
- `Reset settings`
  Restores the settings file to its defaults
- `Exit`

## Requirements

- Windows x64
- `.NET 10`
- `WPF`

## Dependencies

This repository currently does not depend on third-party NuGet packages or bundled external libraries.  
See [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) for details.

Notes:

- Keyboard input detection uses both a low-level Windows keyboard hook and polling. Detection may be unreliable on x86, so the supported target is x64 only.
- The app normally runs with standard user privileges. Due to Windows privilege isolation, repeated `Ctrl` presses cannot be detected from a standard user process while an elevated window such as Task Manager is active. If needed, run CtrlHanabi as administrator or deploy it as a signed `uiAccess` app in a trusted folder.

## Build and Run

Build and run for x64 with:

```powershell
dotnet build
dotnet run
```

## Settings File

Location:

`%LocalAppData%\CtrlHanabi\settings.json`

Default values:

```json
{
  "DoubleTapThresholdMs": 320,
  "CooldownMs": 500,
  "ParticleCount": 90,
  "ExplosionRadius": 110,
  "HourlyStarmineEnabled": false,
  "StarmineLaneLeftEnabled": true,
  "StarmineLaneCenterEnabled": true,
  "StarmineLaneRightEnabled": true,
  "StarmineDisplayIndex": 1,
  "UiLanguage": null
}
```

Meaning of each field:

- `DoubleTapThresholdMs`
  Maximum interval treated as repeated `Ctrl` input for double-tap and triple-tap detection
- `CooldownMs`
  Cooldown time used to prevent consecutive triggers
- `ParticleCount`
  Base number of firework particles
- `ExplosionRadius`
  Base explosion radius
- `HourlyStarmineEnabled`
  Whether to launch a starmine every hour at `59:30`
- `StarmineLaneLeftEnabled`
  Whether the left starmine lane is enabled
- `StarmineLaneCenterEnabled`
  Whether the center starmine lane is enabled
- `StarmineLaneRightEnabled`
  Whether the right starmine lane is enabled
- `StarmineDisplayIndex`
  Display number used for starmine playback, starting at `1`. Out-of-range values are treated as `1`
  `1` is the main display. `2` and above select the remaining displays in desktop order
- `UiLanguage`
  Menu language. If `null`, omitted, or `"auto"`, the value is inferred from the Windows UI language. Set `"ja"` or `"en"` to force a language

Notes:

- There is no settings UI, so edit `settings.json` directly when needed.

## Logging Configuration

Direct3D-related logs are written to `%LocalAppData%\CtrlHanabi\d3d11.log`.  
Logging is controlled by these environment variables:

- `CTRLHANABI_LOG`
  Global logging switch. `1` enables logging, `0` disables it
- `CTRLHANABI_D3D11_LOG`
  Compatibility switch. Used only when `CTRLHANABI_LOG` is not set, and enabled with `1`

Priority:

1. If `CTRLHANABI_LOG=0`, logging is always disabled
2. If `CTRLHANABI_LOG=1`, logging is enabled
3. If `CTRLHANABI_LOG` is unset, `CTRLHANABI_D3D11_LOG` is used
