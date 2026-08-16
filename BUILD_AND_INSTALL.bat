@echo off
setlocal
cd /d "%~dp0"
echo ============================================================
echo CRAFTING / FORAGING CURRENT-SOURCE BUILD + TEST + INSTALL
echo ============================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BUILD_AND_INSTALL.ps1" %*
if errorlevel 1 goto :fail
echo.
echo ============================================================
echo CRAFTING / FORAGING BUILD AND INSTALL COMPLETED SUCCESSFULLY
echo ============================================================
echo.
pause
exit /b 0
:fail
echo.
echo ############################################################
echo #                                                          #
echo #   CRAFTING / FORAGING BUILD AND INSTALL FAILED          #
echo #                                                          #
echo ############################################################
echo.
echo Review the PowerShell error above. No successful install is claimed.
echo.
pause
exit /b 1
