' Launch watchdog.ps1 with no window at all.
' wscript.exe is a GUI-subsystem host, so nothing flashes on screen — unlike a
' powershell.exe task action, whose console window briefly becomes visible even
' with -WindowStyle Hidden (that flash steals focus from fullscreen games).
Dim fso, dir
Set fso = CreateObject("Scripting.FileSystemObject")
dir = fso.GetParentFolderName(WScript.ScriptFullName)
CreateObject("Wscript.Shell").Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -File """ & dir & "\watchdog.ps1""", 0, False
