# AutoKey

A small Windows desktop app that repeats a keyboard key at a chosen interval.

## Download and run

[Download AutoKey v2](https://github.com/YuanSol30/AutoKey/raw/refs/heads/main/AutoKey-v2.exe), then open it on Windows. No installer is needed. Close any older AutoKey instance first.

1. Choose the key and interval in seconds.
2. Set a countdown and optional press limit (0 means unlimited).
3. Leave **Hold key** at 100 milliseconds, or adjust it.
4. Click **Start** or press **F8**, then focus your target application.
5. Press **F9** to stop from any window. F8 also toggles start/stop.

AutoKey pauses while its own window is active. Input goes to the currently active window. If the hold duration exceeds the interval, it leaves at least 20 milliseconds between presses. Timing is approximate.

## Compatibility

Uses Windows scan-code input with separate key-down and key-up events. Roblox compatibility has **not** been verified. Applications can reject simulated input; a sent counter does not prove that an application accepted a key. The final local test launch was blocked by Windows Application Control. This executable is unsigned.

## Build from source

The app uses C# and Windows Forms with .NET Framework. From PowerShell in this directory:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /out:AutoKey-v2.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll AutoKey.cs
```

`AutoKey-v2.exe --self-test` checks input structure sizes and several key mappings. It does not test game compatibility.
