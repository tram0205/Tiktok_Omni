@echo off
setlocal EnableDelayedExpansion
REM Mot lan: init Git (neu chua co) + commit + gan remote + push len GitHub
REM Can: Git for Windows https://git-scm.com/download/win
cd /d "%~dp0..\.."

set "GITEXE=git"
where git >nul 2>nul
if errorlevel 1 (
  if exist "%ProgramFiles%\Git\cmd\git.exe" (
    set "GITEXE=%ProgramFiles%\Git\cmd\git.exe"
  ) else (
    echo.
    echo LOI: Chua cai Git for Windows.
    echo   1^) Cai tu https://git-scm.com/download/win
    echo   2^) Dong mo lai Cursor / terminal
    echo   3^) Chay lai file nay
    echo.
    pause
    exit /b 1
  )
)

if not exist ".git" (
  echo Khoi tao Git repository...
  "%GITEXE%" init
  if errorlevel 1 exit /b 1
)

"%GITEXE%" add -A
"%GITEXE%" diff --cached --quiet
if errorlevel 1 (
  echo Tao commit...
  "%GITEXE%" commit -m "Initial commit: tiktok_Omni"
  if errorlevel 1 (
    echo Commit that bai. Kiem tra user.name / user.email:
    echo   git config --global user.name "Ten ban"
    echo   git config --global user.email "email@github.com"
    pause
    exit /b 1
  )
) else (
  echo Khong co thay doi moi de commit - tiep tuc push.
)

"%GITEXE%" branch -M main 2>nul

echo.
echo --- Buoc tiep: tao repo trong tren GitHub ---
echo   Double-click: github-mo-trang-tao-repo.cmd
echo   Ten goi y: tiktok_Omni  ^(KHONG tick README / .gitignore^)
echo   Copy URL HTTPS: https://github.com/BAN/tiktok_Omni.git
echo.
set /p REPOURL="Dan URL repo vao day (Enter de bo qua, push sau bang github-gan-remote-va-push.cmd): "
if "!REPOURL!"=="" (
  echo Da commit cuc bo. Chay github-gan-remote-va-push.cmd khi co URL.
  pause
  exit /b 0
)

"%GITEXE%" remote get-url origin >nul 2>nul
if errorlevel 1 (
  "%GITEXE%" remote add origin "!REPOURL!"
) else (
  "%GITEXE%" remote set-url origin "!REPOURL!"
)

echo Dang push len GitHub...
"%GITEXE%" push -u origin main
if errorlevel 1 (
  echo Push that bai - xem HUONG_DAN_REPO_GITHUB.txt
  pause
  exit /b 1
)

echo XONG - code da tren GitHub.
pause
endlocal
