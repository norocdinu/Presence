@echo off
REM Builds the SLEEP-ONLY edition: keep-awake only, NO synthetic input.
REM Defines SLEEP_ONLY so all SendInput/jiggler code is compiled out.
REM Output goes to .\sleep-only\Presence.exe (separate from the full build).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist sleep-only mkdir sleep-only

"%CSC%" /nologo /target:winexe /optimize+ /define:SLEEP_ONLY ^
  /out:sleep-only\Presence.exe ^
  /win32manifest:Presence.manifest ^
  /reference:System.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  Presence.cs

if %ERRORLEVEL%==0 (
  echo Build OK -^> sleep-only\Presence.exe
) else (
  echo Build FAILED
)
endlocal
