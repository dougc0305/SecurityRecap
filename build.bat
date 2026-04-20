@echo off
setlocal EnableExtensions DisableDelayedExpansion

REM =====================================================
REM SecurityRecap Build Script
REM =====================================================
REM Builds client + server, creates a zip artifact for deployment.
REM No branch naming convention required - uses current branch.
REM =====================================================

REM =====================================================
REM Where artifacts go
REM =====================================================
set "ARTIFACT_ROOT=C:\Deployments\SecurityRecap\Artifacts"

REM Stable timestamp for folder/file names
for /f "delims=" %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "BUILD_ID=%%i"

REM Detect repo root from this script location
set "SCRIPT_DIR=%~dp0"
pushd "%SCRIPT_DIR%" >nul
for /f "delims=" %%r in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%r"
popd >nul

if "%REPO_ROOT%"=="" (
  echo ERROR: Could not detect git repo root. Make sure this script is inside the repo.
  exit /b 1
)

if not exist "%REPO_ROOT%\.git" (
  echo ERROR: Repo root does not look like a git repo: "%REPO_ROOT%"
  exit /b 1
)

REM =====================================================
REM Solution folder (repo root IS the solution root)
REM =====================================================
set "SOLUTION_DIR=%REPO_ROOT%"

if not exist "%SOLUTION_DIR%\frontend\package.json" (
  echo ERROR: Could not find client at: "%SOLUTION_DIR%\frontend"
  exit /b 1
)

if not exist "%SOLUTION_DIR%\src\SecurityRecap.Api" (
  echo ERROR: Could not find server at: "%SOLUTION_DIR%\src\SecurityRecap.Api"
  exit /b 1
)

REM =====================================================
REM Get current branch
REM =====================================================
pushd "%SOLUTION_DIR%" >nul
for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD 2^>nul') do set "BRANCH=%%b"
popd >nul

if "%BRANCH%"=="" (
  echo ERROR: Could not determine current git branch.
  exit /b 1
)

if /I "%BRANCH%"=="HEAD" (
  echo ERROR: Git is in a detached HEAD state. Check out a branch before building.
  exit /b 1
)

REM =====================================================
REM Build paths
REM =====================================================
set "OUT_DIR=%ARTIFACT_ROOT%\%BRANCH%\%BUILD_ID%"
set "ZIP_PATH=%OUT_DIR%\SecurityRecap_%BRANCH%_%BUILD_ID%.zip"

REM Summary + confirm
echo.
echo =======================
echo Build Summary
echo =======================
echo Repo Root:  "%REPO_ROOT%"
echo Solution:   "%SOLUTION_DIR%"
echo Branch:     "%BRANCH%"
echo Output Dir: "%OUT_DIR%"
echo Zip Path:   "%ZIP_PATH%"
echo.

set /p "CONFIRM_ALL=Proceed with build? (Y/N): "
if /I not "%CONFIRM_ALL%"=="Y" (
  echo Aborted.
  exit /b 1
)

REM Create folders
mkdir "%OUT_DIR%\client" >nul 2>&1
mkdir "%OUT_DIR%\server" >nul 2>&1

REM =====================================================
REM Git sync - require clean working tree
REM =====================================================
pushd "%SOLUTION_DIR%"

for /f "delims=" %%s in ('git status --porcelain 2^>nul') do (
  echo ERROR: Working tree is not clean. Commit/stash changes before building.
  echo.
  echo git status:
  git status
  popd
  exit /b 1
)

echo Fetching...
git fetch --all
if errorlevel 1 (
  echo ERROR: git fetch failed.
  popd
  exit /b 1
)

echo Pulling latest for %BRANCH%...
git pull
if errorlevel 1 (
  echo ERROR: git pull failed.
  popd
  exit /b 1
)

REM Record commit hash
for /f %%i in ('git rev-parse HEAD') do set "GIT_SHA=%%i"
echo %GIT_SHA%> "%OUT_DIR%\git_sha.txt"

REM =====================================================
REM Build Client
REM =====================================================
echo.
echo === Building client ===
pushd "frontend"

call npm ci
if errorlevel 1 (
  echo ERROR: npm ci failed.
  popd
  popd
  exit /b 1
)

call npm run build
if errorlevel 1 (
  echo ERROR: npm run build failed.
  popd
  popd
  exit /b 1
)

echo Copying dist to artifact...
robocopy ".\dist" "%OUT_DIR%\client" /E /NFL /NDL /NJH /NJS /NC /NS
if errorlevel 8 (
  echo ERROR: robocopy client failed.
  popd
  popd
  exit /b 1
)

popd

REM =====================================================
REM Build Server
REM =====================================================
echo.
echo === Publishing server ===
pushd "src\SecurityRecap.Api"

dotnet restore
if errorlevel 1 (
  echo ERROR: dotnet restore failed.
  popd
  popd
  exit /b 1
)

dotnet publish -c Release -o "%OUT_DIR%\server"
if errorlevel 1 (
  echo ERROR: dotnet publish failed.
  popd
  popd
  exit /b 1
)

popd
popd

REM =====================================================
REM Zip it
REM =====================================================
echo.
echo === Creating zip artifact ===

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "New-Item -ItemType Directory -Force -Path '%OUT_DIR%' | Out-Null; " ^
  "if (Test-Path '%ZIP_PATH%') { Remove-Item -Force '%ZIP_PATH%' }; " ^
  "Compress-Archive -Path '%OUT_DIR%\client','%OUT_DIR%\server','%OUT_DIR%\git_sha.txt' -DestinationPath '%ZIP_PATH%'"

if errorlevel 1 (
  echo ERROR: Compress-Archive failed.
  exit /b 1
)

REM =====================================================
REM Done
REM =====================================================
echo.
echo DONE.
echo Artifact zip:
echo   %ZIP_PATH%
echo.
echo Current branch:
git -C "%SOLUTION_DIR%" rev-parse --abbrev-ref HEAD
echo.
endlocal
exit /b 0
