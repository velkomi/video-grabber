; VideoGrabber Windows installer — NSIS 3.12
Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64
CRCCheck on
AutoCloseWindow false
ShowInstDetails show
ShowUninstDetails show

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"
!include "Sections.nsh"
!include "nsDialogs.nsh"

!ifndef RELEASE_DIR
  !error "RELEASE_DIR is required"
!endif
!ifndef OUTPUT_DIR
  !define OUTPUT_DIR "."
!endif
!ifndef APP_VERSION_TEXT
  !define APP_VERSION_TEXT "0.0.0"
!endif
!ifndef APP_VERSION_NUMERIC
  !define APP_VERSION_NUMERIC "1.0.0.0"
!endif

!define APP_NAME "VideoGrabber"
!define APP_PUBLISHER "Валерий Канев"
!define APP_URL "https://videograbber.srv1902378.hstgr.cloud/"
!define APP_EXE "VideoGrabber.Managed.exe"
!define APP_UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoGrabber"

Name "${APP_NAME}"
OutFile "${OUTPUT_DIR}\VideoGrabber-Setup.exe"
InstallDir "$PROGRAMFILES64\VideoGrabber"
InstallDirRegKey HKLM "${APP_UNINSTALL_KEY}" "InstallLocation"
BrandingText "VideoGrabber"
Icon "${RELEASE_DIR}\Assets\VideoGrabber.ico"
UninstallIcon "${RELEASE_DIR}\Assets\VideoGrabber.ico"
VIProductVersion "${APP_VERSION_NUMERIC}"
VIAddVersionKey /LANG=1049 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1049 "ProductVersion" "${APP_VERSION_TEXT}"
VIAddVersionKey /LANG=1049 "FileVersion" "${APP_VERSION_NUMERIC}"
VIAddVersionKey /LANG=1049 "FileDescription" "Установщик VideoGrabber"
VIAddVersionKey /LANG=1049 "CompanyName" "${APP_PUBLISHER}"
VIAddVersionKey /LANG=1049 "LegalCopyright" "© ${APP_PUBLISHER}"

!define MUI_ABORTWARNING
!define MUI_ICON "${RELEASE_DIR}\Assets\VideoGrabber.ico"
!define MUI_UNICON "${RELEASE_DIR}\Assets\VideoGrabber.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Запустить VideoGrabber"
!define MUI_COMPONENTSPAGE_SMALLDESC

!insertmacro MUI_PAGE_WELCOME
Page custom InstallModePage InstallModeLeave

!define MUI_PAGE_CUSTOMFUNCTION_PRE DirectoryPre
!insertmacro MUI_PAGE_DIRECTORY

!define MUI_PAGE_CUSTOMFUNCTION_PRE ComponentsPre
!insertmacro MUI_PAGE_COMPONENTS

!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "Russian"

Var InstallMode
Var ModeDialog
Var StandardRadio
Var AdvancedRadio
Var ModeNote

Section "Основное приложение (обязательно)" SecCore
  SectionIn RO

  DetailPrint "Закрываю запущенные экземпляры VideoGrabber перед обновлением..."
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /IM "VideoGrabber.Managed.exe"'
  Pop $0
  Pop $1
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /IM "VideoGrabber.exe"'
  Pop $0
  Pop $1
  Sleep 1500

  SetOutPath "$INSTDIR"
  File /r "${RELEASE_DIR}\*.*"

  SetOutPath "$INSTDIR\tools"
  File /oname=install-whisper-model.ps1 "${__FILEDIR__}\install-whisper-model.ps1"
  SetOutPath "$INSTDIR"

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION_TEXT}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr HKLM "${APP_UNINSTALL_KEY}" "URLInfoAbout" "${APP_URL}"
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${APP_UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Встроенная модель Base — быстро, ~141 МБ (обязательно)" SecBase
  SectionIn RO
SectionEnd

Section "Загрузка, редактор и встроенный браузер (обязательно)" SecRuntime
  SectionIn RO
SectionEnd

Section /o "Модель Small — оптимально для курсов и русской речи, ~181 МБ" SecSmall
  DetailPrint "Проверяю/скачиваю модель Small..."
  nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\tools\install-whisper-model.ps1" -Profile small'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_ICONEXCLAMATION|MB_OK "Small не удалось скачать сейчас. VideoGrabber установлен нормально; модель можно выбрать и скачать позже прямо в приложении."
  ${EndIf}
SectionEnd

Section /o "Модель Medium — максимальное качество, ~514 МБ" SecMedium
  DetailPrint "Проверяю/скачиваю модель Medium..."
  nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "$INSTDIR\tools\install-whisper-model.ps1" -Profile medium'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_ICONEXCLAMATION|MB_OK "Medium не удалось скачать сейчас. VideoGrabber установлен нормально; модель можно выбрать и скачать позже прямо в приложении."
  ${EndIf}
SectionEnd

Section "Ярлык «VideoGrabber» на рабочем столе" SecDesktop
  SetShellVarContext all
  CreateShortcut "$DESKTOP\VideoGrabber.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
SectionEnd

Section "Папка VideoGrabber в меню Пуск" SecStartMenu
  SetShellVarContext all
  CreateDirectory "$SMPROGRAMS\VideoGrabber"
  CreateShortcut "$SMPROGRAMS\VideoGrabber\VideoGrabber.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortcut "$SMPROGRAMS\VideoGrabber\Удалить VideoGrabber.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} "Основные файлы VideoGrabber. Устанавливаются в Program Files."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecBase} "Минимальная встроенная модель транскрибации. Работает без дополнительной загрузки."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecRuntime} "Компоненты загрузки/редактирования/браузера и локальной обработки видео."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecSmall} "Скачивается один раз из открытого источника и сохраняется в локальном кэше пользователя. Рекомендуемый баланс качества и скорости."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecMedium} "Скачивается один раз. Лучше для сложной речи и шумного звука, но работает медленнее и занимает больше места."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} "Создать обычный ярлык «VideoGrabber» на общем рабочем столе Windows."
  !insertmacro MUI_DESCRIPTION_TEXT ${SecStartMenu} "Создать папку «VideoGrabber» в меню Пуск с ярлыком программы и ярлыком удаления."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  DetailPrint "Закрываю запущенные экземпляры VideoGrabber перед удалением..."
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /IM "VideoGrabber.Managed.exe"'
  Pop $0
  Pop $1
  nsExec::ExecToStack '"$SYSDIR\taskkill.exe" /F /T /IM "VideoGrabber.exe"'
  Pop $0
  Pop $1
  Sleep 1500

  SetShellVarContext all
  Delete "$DESKTOP\VideoGrabber.lnk"
  Delete "$SMPROGRAMS\VideoGrabber\VideoGrabber.lnk"
  Delete "$SMPROGRAMS\VideoGrabber\Удалить VideoGrabber.lnk"
  RMDir "$SMPROGRAMS\VideoGrabber"

  DeleteRegKey HKLM "${APP_UNINSTALL_KEY}"

  RMDir /r "$INSTDIR"

  MessageBox MB_ICONQUESTION|MB_YESNO|MB_DEFBUTTON2 "Удалить также локальные настройки VideoGrabber и скачанные модели транскрибации? Скачанные вами видео и файлы курса в других папках затронуты не будут." IDNO KeepLocalData
    SetShellVarContext current
    RMDir /r "$LOCALAPPDATA\VideoGrabber"
  KeepLocalData:
SectionEnd

Function .onInit
  StrCpy $InstallMode "standard"
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_ICONSTOP|MB_OK "VideoGrabber поддерживает Windows 10 и Windows 11."
    Abort
  ${EndIf}
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP|MB_OK "Для этой версии VideoGrabber требуется 64-разрядная Windows 10/11."
    Abort
  ${EndIf}
  SetRegView 64
FunctionEnd

Function InstallModePage
  nsDialogs::Create 1018
  Pop $ModeDialog
  ${If} $ModeDialog == error
    Abort
  ${EndIf}

  !insertmacro MUI_HEADER_TEXT "Режим установки" "Стандартная установка подходит большинству пользователей."

  ${NSD_CreateRadioButton} 0 8u 100% 16u "Стандартная (рекомендуется)"
  Pop $StandardRadio
  ${NSD_Check} $StandardRadio

  ${NSD_CreateLabel} 18u 27u 92% 36u "VideoGrabber будет установлен в Program Files. Будут созданы ярлык на рабочем столе и папка VideoGrabber в меню Пуск. Встроенная Base-модель уже входит в комплект."
  Pop $ModeNote

  ${NSD_CreateRadioButton} 0 73u 100% 16u "Расширенная"
  Pop $AdvancedRadio

  ${NSD_CreateLabel} 18u 92u 92% 48u "Можно изменить папку установки, отключить отдельные ярлыки и заранее скачать Small (~181 МБ) и/или Medium (~514 МБ). Дополнительные модели всегда можно скачать позже прямо в VideoGrabber."
  Pop $ModeNote

  nsDialogs::Show
FunctionEnd

Function InstallModeLeave
  ${NSD_GetState} $AdvancedRadio $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $InstallMode "advanced"
  ${Else}
    StrCpy $InstallMode "standard"

    SectionGetFlags ${SecDesktop} $1
    IntOp $1 $1 | ${SF_SELECTED}
    SectionSetFlags ${SecDesktop} $1

    SectionGetFlags ${SecStartMenu} $1
    IntOp $1 $1 | ${SF_SELECTED}
    SectionSetFlags ${SecStartMenu} $1

    SectionGetFlags ${SecSmall} $1
    IntOp $1 $1 & ~${SF_SELECTED}
    SectionSetFlags ${SecSmall} $1

    SectionGetFlags ${SecMedium} $1
    IntOp $1 $1 & ~${SF_SELECTED}
    SectionSetFlags ${SecMedium} $1
  ${EndIf}
FunctionEnd

Function DirectoryPre
  ${If} $InstallMode == "standard"
    Abort
  ${EndIf}
FunctionEnd

Function ComponentsPre
  ${If} $InstallMode == "standard"
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd
