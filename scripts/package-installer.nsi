; DoraMate NSIS Installer Script
; Requires NSIS 3.0+ (https://nsis.sourceforge.io)

!define PRODUCT_NAME "DoraMate"
!define PRODUCT_VERSION "0.9.0"
!define PRODUCT_PUBLISHER "DoraMate Contributors"
!define PRODUCT_WEB_SITE "https://github.com/dora-rs/doramate"
!define PRODUCT_DIR_REGKEY "Software\Microsoft\Windows\CurrentVersion\App Paths\doramate-localagent.exe"
!define PRODUCT_UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PRODUCT_NAME}"
!define PRODUCT_UNINST_ROOT_KEY "HKLM"

SetCompressor lzma

; MUI (Modern User Interface)
!include "MUI2.nsh"
!include "FileFunc.nsh"

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "${NSISDIR}\Contrib\Graphics\Icons\modern-install.ico"
!define MUI_UNICON "${NSISDIR}\Contrib\Graphics\Icons\modern-uninstall.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${NSISDIR}\Contrib\Graphics\Wizard\win.bmp"

; Installation pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

; Uninstallation pages
!insertmacro MUI_UNPAGE_INSTFILES

; Languages
!insertmacro MUI_LANGUAGE "English"

Name "${PRODUCT_NAME} ${PRODUCT_VERSION}"
OutFile "doramate-${PRODUCT_VERSION}-setup.exe"
InstallDir "$PROGRAMFILES64\DoraMate"
InstallDirRegKey HKLM "${PRODUCT_DIR_REGKEY}" ""
ShowInstDetails show
ShowUnInstDetails show

Section "MainSection" SEC01
    SetOutPath "$INSTDIR"
    SetOverwrite ifnewer

    ; Binaries
    File "..\target\release\doramate-localagent.exe"
    File "..\dora-api-csharp\third_party\dora\target\release\dora.exe"

    ; Frontend (WASM)
    CreateDirectory "$INSTDIR\frontend"
    File /r "..\doramate-frontend\dist\*"

    ; Examples
    CreateDirectory "$INSTDIR\examples"
    File "..\doramate-examples\*.yml"
    File /nonfatal "..\doramate-examples\*.layout.json"

    ; Start/Stop scripts
    File "..\scripts\start-doramate.cmd"
    File "..\scripts\stop-doramate.cmd"

    ; README
    File "..\README.md"

    ; Shortcuts
    CreateDirectory "$SMPROGRAMS\DoraMate"
    CreateShortCut "$SMPROGRAMS\DoraMate\DoraMate.lnk" "$INSTDIR\start-doramate.cmd" "" "$INSTDIR\doramate-localagent.exe" 0
    CreateShortCut "$SMPROGRAMS\DoraMate\Uninstall.lnk" "$INSTDIR\uninstall.exe" "" "" 0
    CreateShortCut "$DESKTOP\DoraMate.lnk" "$INSTDIR\start-doramate.cmd" "" "$INSTDIR\doramate-localagent.exe" 0

    ; Registry for uninstall
    WriteUninstaller "$INSTDIR\uninstall.exe"
    WriteRegStr HKLM "${PRODUCT_DIR_REGKEY}" "" "$INSTDIR\doramate-localagent.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayName" "$(^Name)"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "UninstallString" "$INSTDIR\uninstall.exe"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "DisplayVersion" "${PRODUCT_VERSION}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
    WriteRegStr ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "URLInfoAbout" "${PRODUCT_WEB_SITE}"
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoModify" 1
    WriteRegDWORD ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}" "NoRepair" 1
SectionEnd

Section -Post
    ; Register PATH (optional)
    Push $INSTDIR
    Call AddToPath
SectionEnd

Function un.onUninstSuccess
    HideWindow
    MessageBox MB_ICONINFORMATION "$(^Name) was successfully removed from your computer."
FunctionEnd

Function un.onInit
    MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Are you sure you want to completely remove $(^Name) and all of its components?" IDYES +2
    Abort
FunctionEnd

Section Uninstall
    ; Remove files
    Delete "$INSTDIR\doramate-localagent.exe"
    Delete "$INSTDIR\dora.exe"
    RMDir /r "$INSTDIR\frontend"
    RMDir /r "$INSTDIR\examples"
    Delete "$INSTDIR\start-doramate.cmd"
    Delete "$INSTDIR\stop-doramate.cmd"
    Delete "$INSTDIR\README.md"
    Delete "$INSTDIR\uninstall.exe"
    RMDir "$INSTDIR"

    ; Remove shortcuts
    Delete "$SMPROGRAMS\DoraMate\DoraMate.lnk"
    Delete "$SMPROGRAMS\DoraMate\Uninstall.lnk"
    RMDir "$SMPROGRAMS\DoraMate"
    Delete "$DESKTOP\DoraMate.lnk"

    ; Remove registry keys
    DeleteRegKey HKLM "${PRODUCT_DIR_REGKEY}"
    DeleteRegKey ${PRODUCT_UNINST_ROOT_KEY} "${PRODUCT_UNINST_KEY}"

    ; Remove PATH
    Push "$INSTDIR"
    Call un.RemoveFromPath
SectionEnd

; ──────────────────────────────────────────────
; Path manipulation functions (from NSIS wiki)
; ──────────────────────────────────────────────

!include "WinMessages.nsh"

Function AddToPath
    Exch $0
    Push $1
    Push $2
    Push $3

    ; Check if already in PATH
    ReadRegStr $1 HKCU "Environment" "PATH"
    Push $1
    Push "$0;"
    Call StrStr
    Pop $2
    StrCmp $2 "" AddToPath_DoIt
    StrLen $2 "$0"
    StrCpy $3 $1 $2
    StrCmp $3 "$0" AddToPath_Done
    StrCpy $3 $1 "" $2
    StrCpy $3 "$0;$3"
    StrCmp $3 "$1" AddToPath_Done
AddToPath_DoIt:
    StrCpy $1 "$1;$0"
    WriteRegStr HKCU "Environment" "PATH" $1
    SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000
AddToPath_Done:
    Pop $3
    Pop $2
    Pop $1
    Pop $0
FunctionEnd

Function un.RemoveFromPath
    Exch $0
    Push $1
    Push $2
    Push $3
    Push $4
    Push $5

    ReadRegStr $1 HKCU "Environment" "PATH"
    StrCpy $5 $1
    StrCpy $2 "$0;"
    Call un.StrStr
    Pop $2
    StrCmp $2 "" un.RemoveFromPath_Done
    ; Remove entry
    StrLen $3 "$0"
    StrLen $4 "$2"
    StrCpy $3 $1 ($4 - $3 - 1)
    WriteRegStr HKCU "Environment" "PATH" $3
    SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000
un.RemoveFromPath_Done:
    Pop $5
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Pop $0
FunctionEnd

; StrStr function
Function StrStr
    Exch $1
    Exch
    Exch $0
    Push $2
    Push $3
    Push $4
    StrLen $2 $0
    StrCmp $2 0 StrStr_Error
    StrCpy $3 $1
    StrLen $4 $1
    StrCmp $4 0 StrStr_Error
    loop:
        StrCpy $4 $3 $2
        StrCmp $4 $0 StrStr_Done
        StrCpy $3 $3 "" 1
        StrLen $4 $3
        StrCmp $4 0 StrStr_Error
        Goto loop
StrStr_Error:
    StrCpy $3 ""
StrStr_Done:
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Exch $3
FunctionEnd

Function un.StrStr
    Exch $1
    Exch
    Exch $0
    Push $2
    Push $3
    Push $4
    StrLen $2 $0
    StrCmp $2 0 un.StrStr_Error
    StrCpy $3 $1
    StrLen $4 $1
    StrCmp $4 0 un.StrStr_Error
    loop:
        StrCpy $4 $3 $2
        StrCmp $4 $0 un.StrStr_Done
        StrCpy $3 $3 "" 1
        StrLen $4 $3
        StrCmp $4 0 un.StrStr_Error
        Goto loop
un.StrStr_Error:
    StrCpy $3 ""
un.StrStr_Done:
    Pop $4
    Pop $3
    Pop $2
    Pop $1
    Exch $3
FunctionEnd
