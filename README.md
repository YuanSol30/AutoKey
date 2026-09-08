# AutoKey

A Windows desktop app with a minimal white interface that repeats keyboard presses or mouse clicks at a chosen interval.

## Download and run

[Download AutoKey v4](https://github.com/YuanSol30/AutoKey/raw/refs/heads/main/AutoKey-v4.exe), then open it on Windows. No installer is needed. Close any older AutoKey instance first.

1. Choose Keyboard press or Mouse click, select the key or left/right/middle mouse button, and set the interval in milliseconds.
2. Set a countdown and optional press limit (0 means unlimited).
3. Leave **Hold duration** at 30 milliseconds, or adjust it.
4. Click **Start** or press **F8**, then focus your target application.
5. Press **F9** to stop from any window. F8 also toggles start/stop.

AutoKey pauses while its own window is active. Keyboard input goes to the currently active window. Mouse clicks happen at the current cursor position; move the cursor to your target during the countdown. Clicking pauses only when AutoKey is active or the visible window beneath the cursor belongs to AutoKey. A game covering AutoKey no longer causes a false pause. The interval accepts 1 ms or more. Hold duration is automatically shortened to at most half the interval (minimum 1 ms), followed by at least a 1 ms release gap. Very short requested intervals may run slower; Windows scheduling and the target application determine the actual rate.

## Compatibility

Uses Windows scan-code input with separate key-down and key-up events. Roblox compatibility has **not** been verified. Applications can reject simulated input; a sent counter does not prove that an application accepted a key. Local v4 input mapping, release, pause detection, and hold timing checks passed; the interface was rendered and visually checked. This executable is unsigned.

## Build from source

The app uses C# and Windows Forms with .NET Framework. From PowerShell in this directory:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:AutoKey-v4.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll AutoKey.cs
```

`AutoKey-v4.exe --self-test` checks input structure sizes, key mappings, left/right/middle button release flags, covered-window pause behavior, and short-interval hold duration. It does not test game compatibility.


