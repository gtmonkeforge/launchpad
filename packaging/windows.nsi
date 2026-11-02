Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!ifndef APP_VERSION
  !error "Pass /DAPP_VERSION=<version> (or -DAPP_VERSION=<version> on Linux)"
!endif
Name "MonkeForge Launchpad"
OutFile "../dist/launchpad-${APP_VERSION}-windows-x64-setup.exe"
InstallDir "$LOCALAPPDATA\Programs\MonkeForge Launchpad"
InstallDirRegKey HKCU "Software\MonkeForge\Launchpad" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Icon "../logo.ico"
UninstallIcon "../logo.ico"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "MonkeForge Launchpad"
VIAddVersionKey /LANG=1033 "FileDescription" "MonkeForge Launchpad Installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright 2026 MonkeForge"
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "MonkeForge Launchpad requires 64-bit Windows."
    Abort
  ${EndIf}
FunctionEnd
Section "Install"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File "../dist/launchpad-win64.exe"
  File "../LICENSE.md"
  WriteUninstaller "$INSTDIR\uninstall.exe"
  CreateDirectory "$SMPROGRAMS"
  CreateShortcut "$SMPROGRAMS\MonkeForge Launchpad.lnk" "$INSTDIR\launchpad-win64.exe" "" "$INSTDIR\launchpad-win64.exe" 0
  WriteRegStr HKCU "Software\MonkeForge\Launchpad" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "DisplayName" "MonkeForge Launchpad"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "Publisher" "MonkeForge"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "DisplayIcon" "$INSTDIR\launchpad-win64.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "UninstallString" '$\"$INSTDIR\uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "QuietUninstallString" '$\"$INSTDIR\uninstall.exe$\" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad" "NoRepair" 1
SectionEnd
Section "Uninstall"
  SetShellVarContext current
  Delete "$SMPROGRAMS\MonkeForge Launchpad.lnk"
  Delete "$SMSTARTUP\MonkeForge Launchpad.vbs"
  Delete "$INSTDIR\launchpad-win64.exe"
  Delete "$INSTDIR\LICENSE.md"
  Delete "$INSTDIR\uninstall.exe"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\MonkeForge\Launchpad"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\MonkeForgeLaunchpad"
SectionEnd
