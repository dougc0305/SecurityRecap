@echo off
setlocal EnableExtensions DisableDelayedExpansion

REM =====================================================
REM SecurityRecap Deploy Script
REM =====================================================
REM Takes a branch name, finds the latest artifact zip
REM under C:\Deployments\SecurityRecap\Artifacts\{branch},
REM stops IIS site, deploys server + client files, and
REM restarts the site.
REM
REM Run as Administrator (required for IIS commands).
REM =====================================================

REM =====================================================
REM Configuration - adjust these per environment
REM =====================================================
set "IIS_SITE_NAME=SecurityRecap"
set "DEPLOY_ROOT=C:\inetpub\wwwroot\SecurityRecap"
set "BACKUP_ROOT=C:\Deployments\SecurityRecap\Backups"
set "ARTIFACT_ROOT=C:\Deployments\SecurityRecap\Artifacts"

REM =====================================================
REM Parse arguments
REM =====================================================
if "%~1"=="" (
  echo Usage: deploy.bat ^<branch-name^>
  echo.
  echo Example: deploy.bat main
  echo          deploy.bat Development
  echo.
  echo Finds the latest artifact zip under:
  echo   %ARTIFACT_ROOT%\^<branch^>\^<latest-timestamp^>\
  exit /b 1
)

set "BRANCH=%~1"
set "BRANCH_DIR=%ARTIFACT_ROOT%\%BRANCH%"

if not exist "%BRANCH_DIR%" (
  echo ERROR: No artifacts found for branch "%BRANCH%"
  echo Looked in: "%BRANCH_DIR%"
  echo.
  echo Available branches:
  dir /AD /B "%ARTIFACT_ROOT%" 2>nul
  exit /b 1
)

REM =====================================================
REM Find latest artifact (most recent timestamp folder)
REM =====================================================
set "LATEST_DIR="
for /f "delims=" %%d in ('dir /AD /B /O-N "%BRANCH_DIR%" 2^>nul') do (
  if not defined LATEST_DIR set "LATEST_DIR=%%d"
)

if not defined LATEST_DIR (
  echo ERROR: No timestamp folders found under "%BRANCH_DIR%"
  exit /b 1
)

set "BUILD_DIR=%BRANCH_DIR%\%LATEST_DIR%"

REM Find the zip file in that folder
set "ZIP_PATH="
for /f "delims=" %%z in ('dir /B "%BUILD_DIR%\*.zip" 2^>nul') do (
  if not defined ZIP_PATH set "ZIP_PATH=%BUILD_DIR%\%%z"
)

if not defined ZIP_PATH (
  echo ERROR: No zip file found in "%BUILD_DIR%"
  exit /b 1
)

REM Timestamp for backup
for /f "delims=" %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "DEPLOY_ID=%%i"

set "BACKUP_DIR=%BACKUP_ROOT%\%DEPLOY_ID%"

REM =====================================================
REM Summary + confirm
REM =====================================================
echo.
echo =======================
echo Deploy Summary
echo =======================
echo Branch:      %BRANCH%
echo Build:       %LATEST_DIR%
echo IIS Site:    %IIS_SITE_NAME%
echo Deploy Root: %DEPLOY_ROOT%
echo Artifact:    %ZIP_PATH%
echo Backup Dir:  %BACKUP_DIR%
echo.

set /p "CONFIRM=Proceed with deployment? (Y/N): "
if /I not "%CONFIRM%"=="Y" (
  echo Aborted.
  exit /b 1
)

REM =====================================================
REM Check for admin privileges (needed for IIS commands)
REM =====================================================
net session >nul 2>&1
if errorlevel 1 (
  echo ERROR: This script must be run as Administrator.
  exit /b 1
)

REM =====================================================
REM Create temp extract directory
REM =====================================================
set "EXTRACT_DIR=%TEMP%\SecurityRecap_deploy_%DEPLOY_ID%"
mkdir "%EXTRACT_DIR%" >nul 2>&1

echo.
echo === Extracting artifact ===
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Expand-Archive -Path '%ZIP_PATH%' -DestinationPath '%EXTRACT_DIR%' -Force"

if errorlevel 1 (
  echo ERROR: Failed to extract zip.
  exit /b 1
)

REM Verify extracted contents
if not exist "%EXTRACT_DIR%\server" (
  echo ERROR: Extracted archive missing 'server' folder.
  exit /b 1
)
if not exist "%EXTRACT_DIR%\client" (
  echo ERROR: Extracted archive missing 'client' folder.
  exit /b 1
)

REM =====================================================
REM Preserve operator-managed files across deploys
REM (these live on the server only, never in the artifact)
REM =====================================================
set "PRESERVE_DIR=%TEMP%\SecurityRecap_preserve_%DEPLOY_ID%"
mkdir "%PRESERVE_DIR%" >nul 2>&1

if exist "%DEPLOY_ROOT%\appsettings.Production.json" (
  echo Preserving appsettings.Production.json...
  copy /Y "%DEPLOY_ROOT%\appsettings.Production.json" "%PRESERVE_DIR%\appsettings.Production.json" >nul
)

REM =====================================================
REM Backup current deployment
REM =====================================================
if exist "%DEPLOY_ROOT%" (
  echo.
  echo === Backing up current deployment ===
  mkdir "%BACKUP_DIR%" >nul 2>&1
  robocopy "%DEPLOY_ROOT%" "%BACKUP_DIR%" /E /NFL /NDL /NJH /NJS /NC /NS /MOVE >nul 2>&1
  echo Backup saved to: %BACKUP_DIR%
) else (
  echo No existing deployment to back up.
  mkdir "%DEPLOY_ROOT%" >nul 2>&1
)

REM =====================================================
REM Stop IIS site
REM =====================================================
echo.
echo === Stopping IIS site: %IIS_SITE_NAME% ===

REM Check if site exists first
%windir%\system32\inetsrv\appcmd list site /name:"%IIS_SITE_NAME%" >nul 2>&1
if errorlevel 1 (
  echo WARNING: IIS site '%IIS_SITE_NAME%' not found. Will create deployment folder only.
  echo You will need to create the IIS site manually and point it to: %DEPLOY_ROOT%
  set "SITE_EXISTS=0"
) else (
  %windir%\system32\inetsrv\appcmd stop site /site.name:"%IIS_SITE_NAME%" >nul 2>&1
  %windir%\system32\inetsrv\appcmd stop apppool /apppool.name:"%IIS_SITE_NAME%" >nul 2>&1
  echo Site stopped.
  set "SITE_EXISTS=1"
  REM Give IIS a moment to release file locks
  timeout /t 3 /nobreak >nul
)

REM =====================================================
REM Deploy server files
REM =====================================================
echo.
echo === Deploying server files ===
mkdir "%DEPLOY_ROOT%" >nul 2>&1
robocopy "%EXTRACT_DIR%\server" "%DEPLOY_ROOT%" /E /NFL /NDL /NJH /NJS /NC /NS
if errorlevel 8 (
  echo ERROR: Failed to copy server files.
  goto restore_and_exit
)

REM =====================================================
REM Deploy client files into wwwroot
REM =====================================================
echo.
echo === Deploying client files to wwwroot ===
mkdir "%DEPLOY_ROOT%\wwwroot" >nul 2>&1
robocopy "%EXTRACT_DIR%\client" "%DEPLOY_ROOT%\wwwroot" /E /NFL /NDL /NJH /NJS /NC /NS
if errorlevel 8 (
  echo ERROR: Failed to copy client files.
  goto restore_and_exit
)

REM =====================================================
REM Copy git SHA for reference
REM =====================================================
if exist "%EXTRACT_DIR%\git_sha.txt" (
  copy /Y "%EXTRACT_DIR%\git_sha.txt" "%DEPLOY_ROOT%\git_sha.txt" >nul
)

REM =====================================================
REM Restore preserved operator-managed files
REM =====================================================
if exist "%PRESERVE_DIR%\appsettings.Production.json" (
  echo Restoring appsettings.Production.json...
  copy /Y "%PRESERVE_DIR%\appsettings.Production.json" "%DEPLOY_ROOT%\appsettings.Production.json" >nul
)
rmdir /S /Q "%PRESERVE_DIR%" >nul 2>&1

REM =====================================================
REM Start IIS site
REM =====================================================
if "%SITE_EXISTS%"=="1" (
  echo.
  echo === Starting IIS site: %IIS_SITE_NAME% ===
  %windir%\system32\inetsrv\appcmd start apppool /apppool.name:"%IIS_SITE_NAME%"
  %windir%\system32\inetsrv\appcmd start site /site.name:"%IIS_SITE_NAME%"
  echo Site started.
)

REM =====================================================
REM Cleanup
REM =====================================================
rmdir /S /Q "%EXTRACT_DIR%" >nul 2>&1

echo.
echo =======================
echo DEPLOY COMPLETE
echo =======================
echo Branch:      %BRANCH%
echo Build:       %LATEST_DIR%
echo Deployed to: %DEPLOY_ROOT%
if exist "%DEPLOY_ROOT%\git_sha.txt" (
  echo Git SHA:
  type "%DEPLOY_ROOT%\git_sha.txt"
)
echo.
goto end_script

:restore_and_exit
echo.
echo ERROR: Deployment failed. Attempting to restore backup...
if exist "%BACKUP_DIR%" (
  robocopy "%BACKUP_DIR%" "%DEPLOY_ROOT%" /E /NFL /NDL /NJH /NJS /NC /NS /MOVE >nul 2>&1
  echo Backup restored.
)
if "%SITE_EXISTS%"=="1" (
  %windir%\system32\inetsrv\appcmd start apppool /apppool.name:"%IIS_SITE_NAME%"
  %windir%\system32\inetsrv\appcmd start site /site.name:"%IIS_SITE_NAME%"
)
exit /b 1

:end_script
endlocal
exit /b 0
