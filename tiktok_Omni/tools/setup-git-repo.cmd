@echo off
setlocal
REM Chạy sau khi cài Git for Windows: https://git-scm.com/download/win
set "GITEXE=git"
where git >nul 2>nul
if errorlevel 1 (
  if exist "%ProgramFiles%\Git\cmd\git.exe" (
    set "GITEXE=%ProgramFiles%\Git\cmd\git.exe"
  ) else (
    echo LOI: Khong tim thay git.exe. Hay cai Git for Windows hoac mo lai terminal sau khi cai.
    exit /b 1
  )
)

cd /d "%~dp0..\.."
if not exist ".git" (
  "%GITEXE%" init
  if errorlevel 1 exit /b 1
)
"%GITEXE%" add -A
"%GITEXE%" status
echo.
echo Tiep theo (neu chua commit):
echo   "%GITEXE%" commit -m "Initial commit"
echo   "%GITEXE%" branch -M main
echo   "%GITEXE%" remote add origin https://github.com/BAN/ten-repo.git
echo   "%GITEXE%" push -u origin main
endlocal
