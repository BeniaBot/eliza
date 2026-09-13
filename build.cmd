@echo off
setlocal
rem  Builds ELIZA with the C# compiler that ships inside Windows.
rem  Nothing is downloaded, nothing is installed, no SDK is required.

cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo Could not find the .NET Framework C# compiler.
    exit /b 1
)

if not exist build\out mkdir build\out
if not exist dist mkdir dist

echo.
echo [1/5] drawing the icon
"%CSC%" -nologo -optimize+ -target:exe -out:build\out\MakeIcon.exe ^
    -reference:System.Drawing.dll build\MakeIcon.cs || exit /b 1
build\out\MakeIcon.exe build\out\eliza.ico || exit /b 1

echo [2/5] regenerating the feminine script
rem
rem   doctor.he.f.txt is generated from doctor.he.txt, ships inside the
rem   executable, and is the script that runs for every woman who uses the
rem   program. Nothing regenerated it and nothing checked it, so a change to
rem   the Hebrew script silently changed the script for men only.
rem
where python >nul 2>nul
if errorlevel 1 (
    echo   python not found: leaving scripts\doctor.he.f.txt as it is
) else (
    python build\make_feminine.py || exit /b 1
)

echo [3/5] building the test runner
"%CSC%" -nologo -optimize+ -target:exe -out:build\out\ElizaTest.exe ^
    src\Eliza.cs src\CaseTable.cs src\Test.cs src\Conversations.cs src\Settings.cs || exit /b 1

echo [4/5] checking against the original
build\out\ElizaTest.exe || (echo CONFORMANCE TESTS FAILED & exit /b 1)

echo.
echo [5/5] building ELIZA.exe
"%CSC%" -nologo -optimize+ -target:winexe -out:dist\ELIZA.exe ^
    -win32icon:build\out\eliza.ico ^
    -reference:System.Drawing.dll -reference:System.Windows.Forms.dll ^
    -reference:Microsoft.VisualBasic.dll ^
    -resource:scripts\doctor.txt,doctor.txt ^
    -resource:scripts\doctor.he.txt,doctor.he.txt ^
    -resource:scripts\doctor.he.f.txt,doctor.he.f.txt ^
    -resource:scripts\rabati.he.txt,rabati.he.txt ^
    -resource:scripts\chikaber.he.txt,chikaber.he.txt ^
    -resource:scripts\mashgiach.he.txt,mashgiach.he.txt ^
    -resource:scripts\shadchan.he.txt,shadchan.he.txt ^
    -resource:scripts\dayan.he.txt,dayan.he.txt ^
    -resource:scripts\tzul.he.txt,tzul.he.txt ^
    src\Eliza.cs src\CaseTable.cs src\Settings.cs src\Shell.cs ^
    src\Ui.cs src\AboutText.cs src\Widgets.cs src\Museum.cs ^
    src\Scenes.cs src\Updater.cs src\Program.cs || exit /b 1

echo.
echo done: dist\ELIZA.exe
dir /b /a-d dist
endlocal
