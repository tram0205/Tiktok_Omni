@echo off
REM Chạy sau khi cài Git for Windows: https://git-scm.com/download/win
cd /d "%~dp0..\.."
if not exist ".git" (
  git init
  if errorlevel 1 goto :nogit
)
git add -A
git status
echo.
echo Tiep theo (tuy chon):
echo   git commit -m "Initial commit"
echo   git branch -M main
echo   git remote add origin https://github.com/BAN/ten-repo.git
echo   git push -u origin main
goto :eof
:nogit
echo LOI: Khong tim thay lenh git. Hay cai Git for Windows va mo lai terminal.
