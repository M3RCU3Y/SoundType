# Manual QA Checklist

Use this checklist for release candidates or UI changes that cannot be fully covered by unit tests.

## App Launch

- App opens on Windows 10/11 without console errors.
- Built-in packs are listed in Sound Library.
- Selecting each built-in pack updates the active-pack status.
- Typing in a normal text field plays the selected pack without noticeable lag.

## Tray Behavior

- Tray icon appears after launch.
- Double-clicking the tray icon opens the app.
- Tray menu shows Open SoundType, Hide to tray, Enabled, active pack, and Exit.
- Hide to tray hides the window and removes it from the taskbar.
- Open SoundType restores the window and taskbar entry.
- With "Keep SoundType running in the tray when closed" enabled, window close hides instead of exits.
- With that setting disabled, window close exits and removes the tray icon.
- Exit from the tray always shuts down the app.

## Windows Startup

- Enabling "Start SoundType when I sign in to Windows" creates the current-user Run entry.
- Disabling it removes the Run entry.
- If registry write fails, the settings panel displays an error and restores the actual checkbox state.

Registry location:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SoundType
```

## Mixer And Equalizer

- Master volume changes overall sound level.
- Normal, Enter, Space, and Backspace sliders affect only their groups.
- Pitch variation makes repeated keys sound less robotic without extreme detune.
- EQ On toggles tone shaping.
- Flat disables EQ trim.
- Warm, Thock, Crisp, and Soft Night presets update all three vertical sliders.
- Moving any EQ slider marks the preset as Custom.

## Pack Workflow

- Importing a valid `.soundpack` adds it to the library.
- Importing an invalid archive reports validation errors and does not partially install.
- Export active creates a `.soundpack` archive.
- Pack validator returns success for built-in packs.

## Privacy Check

- Settings file contains preferences only.
- No typed words, text fields, passwords, or key history are written to disk.
- No network calls are required for typing sounds.
