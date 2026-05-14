@echo off
setlocal
REM Thu muc solution (co file tiktok_Omni.sln) = len 2 cap tu ...\tiktok_Omni\tiktok_Omni\tools
cd /d "%~dp0..\.."

set "GITEXE=C:\Program Files\Git\cmd\git.exe"
if not exist "%GITEXE%" (
  echo Khong tim thay: %GITEXE%
  echo Hay cai Git for Windows hoac sua duong dan trong file .cmd nay.
  pause
  exit /b 1
)

if not exist ".git" (
  echo Chua co Git o day. Chay setup-git-repo.cmd truoc, hoac: git init
  pause
  exit /b 1
)

echo.
echo --- Nhap URL repo HTTPS tu trang GitHub (vi du: https://github.com/me/tiktok_Omni.git) ---
set /p REPOURL="URL: "
if "%REPOURL%"=="" (
  echo Ban chua nhap URL. Thoat.
  pause
  exit /b 1
)

"%GITEXE%" remote get-url origin >nul 2>nul
if errorlevel 1 (
  echo Gan remote ten "origin"...
  "%GITEXE%" remote add origin "%REPOURL%"
) else (
  echo Da co remote "origin" - cap nhat URL...
  "%GITEXE%" remote set-url origin "%REPOURL%"
)

echo.
echo Dang day nhanh main len GitHub (lan dau co the hoi dang nhap)...
"%GITEXE%" push -u origin main
if errorlevel 1 (
  echo.
  echo PUSH THAT BAI. Goi y:
  echo  - Dang nhap github.com tren Chrome/Edge roi thu lai
  echo  - Kiem tra URL dung chua (phai ket thuc .git neu copy tu GitHub)
  echo  - Repo tren GitHub phai trong (khong README commit san neu ban da co lich su khac)
  pause
  exit /b 1
)

echo.
echo XONG. Code da len GitHub. Mo repo tren trinh duyet de kiem tra.
pause
endlocal
