; ================================================
; LemiCraft Launcher - Inno Setup Script
; ================================================

#define AppName "LemiCraft Launcher"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "LemiCraft"
#define AppURL "https://lemicraft.ru"
#define AppExeName "LemiCraft_Launcher.exe"
#define AppId "{{3E8F4A92-1B5C-4D7E-9F2A-8C3D5E6F7A8B}}"

[Setup]
; ================================================
; Basic Information
; ================================================
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/support
AppUpdatesURL={#AppURL}/downloads
AppCopyright=Copyright © 2026 {#AppPublisher}

; ================================================
; Installation Settings
; ================================================
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; ================================================
; Output Settings
; ================================================
OutputDir=..\LemiCraft Launcher\publish
OutputBaseFilename=LemiCraft_Installer
SetupIconFile=..\LemiCraft Launcher\Resources\logo.ico
UninstallDisplayIcon={app}\{#AppExeName}

; ================================================
; Compression
; ================================================
Compression=lzma2/normal
SolidCompression=no
InternalCompressLevel=normal
LZMAUseSeparateProcess=no
;LZMADictionarySize=1048576
;LZMANumFastBytes=273

; ================================================
; UI Settings
; ================================================
WizardStyle=modern
WizardImageFile=compiler:WizClassicImage-IS.bmp
WizardSmallImageFile=compiler:WizClassicSmallImage-IS.bmp
DisableWelcomePage=no
ShowLanguageDialog=no

; ================================================
; Uninstall Settings
; ================================================
Uninstallable=yes
UninstallDisplayName={#AppName}
UninstallFilesDir={app}\uninstall
CreateUninstallRegKey=yes

; ================================================
; Misc Settings
; ================================================
AllowNoIcons=no
AllowCancelDuringInstall=yes
RestartIfNeededByRun=no
CloseApplications=yes
CloseApplicationsFilter=*.exe

; ================================================
; Signing
; ================================================
; SignTool=signtool
; SignedUninstaller=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
russian.WelcomeLabel2=Установка [name/ver] на ваш компьютер.%n%nЭто официальный лаунчер для Minecraft сервера LemiCraft с автоматической установкой модпаков и обновлений
russian.FinishedLabel=Установка [name] завершена.%n%nПрисоединяйтесь к нашему Discord сообществу!

[Messages]
WelcomeLabel1=Добро пожаловать в установку [name]
ClickNext=Нажмите "Далее" для продолжения или "Отмена" для выхода

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Дополнительные значки:"
Name: "quicklaunchicon"; Description: "Создать ярлык в панели быстрого запуска"; GroupDescription: "Дополнительные значки:"; Flags: unchecked

[Files]
; ================================================
; Main Application
; ================================================
Source: "..\LemiCraft Launcher\publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion signonce
;Source: "..\LemiCraft Launcher\publish\*"; Excludes: "*.pdb"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; ================================================
; Start Menu
; ================================================
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Запустить {#AppName}"
Name: "{group}\Открыть папку игры"; Filename: "{%APPDATA}\LemiCraft"; Comment: "Открыть папку с данными"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"; Comment: "Удалить {#AppName}"

; ================================================
; Desktop
; ================================================
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon; Comment: "Запустить {#AppName}"

; ================================================
; Quick Launch
; ================================================
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: quicklaunchicon

[Registry]
; ================================================
; Installation Info
; ================================================
Root: HKCU; Subkey: "Software\LemiCraft"; Flags: uninsdeletekeyifempty
Root: HKCU; Subkey: "Software\LemiCraft\Launcher"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\LemiCraft\Launcher"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"
Root: HKCU; Subkey: "Software\LemiCraft\Launcher"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"
Root: HKCU; Subkey: "Software\LemiCraft\Launcher"; ValueType: string; ValueName: "InstallDate"; ValueData: "{code:GetCurrentDate}"

[Run]
; ================================================
; Post-Installation Actions
; ================================================
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; ================================================
; Pre-Uninstall Actions
; ================================================
Filename: "{cmd}"; Parameters: "/C taskkill /F /IM ""{#AppExeName}"" /T"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; ================================================
; Additional Files to Delete
; ================================================
Type: filesandordirs; Name: "{app}\uninstall"

[Code]
// ================================================
// ФУНКЦИЯ: Получить текущую дату
// ================================================
function GetCurrentDate(Param: String): String;
begin
  Result := GetDateTimeString('yyyy-mm-dd', '-', ':');
end;

// ================================================
// ФУНКЦИЯ: Конвертация Boolean в String
// ================================================
function BoolToStr(B: Boolean): String;
begin
  if B then
    Result := 'true'
  else
    Result := 'false';
end;

// ================================================
// ФУНКЦИЯ: Проверка .NET 8.0+
// ================================================
function IsDotNet80OrHigherInstalled(): Boolean;
var
  Version: String;
  Major, Minor: Integer;
  DotPos: Integer;
  DotNetInstallPath: String;
  FindRec: TFindRec;
  VersionDir: String;
  FoundVersion: Boolean;
begin
  Result := False;
  FoundVersion := False;
  
  // Проверка файловой системы
  DotNetInstallPath := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  
  if not DirExists(DotNetInstallPath) then
  begin
    if RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost') or
       RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost') then
    begin
      Result := True;
      Exit;
    end
    else
      Exit;
  end;
  
  // Ищем папки версий
  if FindFirst(DotNetInstallPath + '\*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Name <> '.') and (FindRec.Name <> '..') and
           (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) then
        begin
          VersionDir := FindRec.Name;
          
          DotPos := Pos('.', VersionDir);
          if DotPos > 0 then
          begin
            Major := StrToIntDef(Copy(VersionDir, 1, DotPos - 1), 0);
            
            if Major >= 8 then
            begin
              Result := True;
              FoundVersion := True;
              Break;
            end;
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
  
  // Резервная проверка через реестр
  if not FoundVersion then
  begin
    if RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost') or
       RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedhost') then
      Result := True;
  end;
end;

// ================================================
// ФУНКЦИЯ: Инициализация установки
// ================================================
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  DotNetUrl: String;
  MsgResult: Integer;
begin
  Result := True;
  
  if not IsDotNet80OrHigherInstalled() then
  begin
    MsgResult := MsgBox(
      'Для работы LemiCraft Launcher требуется .NET 8.0 Desktop Runtime или выше.' + #13#10 + #13#10 +
      'У вас не установлен .NET или установлена устаревшая версия.' + #13#10 + #13#10 +
      'Хотите скачать .NET Desktop Runtime сейчас?' + #13#10 + #13#10 +
      'Да — Открыть страницу загрузки' + #13#10 +
      'Нет — Продолжить установку (приложение может не запуститься)' + #13#10 +
      'Отмена — Прервать установку',
      mbConfirmation, MB_YESNOCANCEL);
    
    case MsgResult of
      IDYES:
        begin
          DotNetUrl := 'https://dotnet.microsoft.com/download/dotnet/8.0/runtime';
          ShellExec('open', DotNetUrl, '', '', SW_SHOW, ewNoWait, ErrorCode);
          
          Result := False;
          
          MsgBox(
            'Страница загрузки .NET открыта в браузере.' + #13#10 + #13#10 +
            'Скачайте ".NET Desktop Runtime x64" (НЕ SDK!)' + #13#10 +
            'После установки запустите установщик LemiCraft Launcher повторно.',
            mbInformation, MB_OK);
        end;
      IDNO:
        begin
          MsgBox(
            'ВНИМАНИЕ: Лаунчер может не запуститься без .NET 8.0!' + #13#10 + #13#10 +
            'Если после установки возникнут проблемы, скачайте .NET с:' + #13#10 +
            'https://dotnet.microsoft.com/download/dotnet/8.0/runtime',
            mbInformation, MB_OK);
          Result := True;
        end;
      IDCANCEL:
        begin
          Result := False;
        end;
    end;
  end;
end;

// ================================================
// ПРОЦЕДУРА: Действия после установки
// ================================================
procedure CurStepChanged(CurStep: TSetupStep);
var
  ConfigFile: String;
  ConfigContent: TStringList;
begin
  if CurStep = ssPostInstall then
  begin
    ConfigFile := ExpandConstant('{userappdata}\LemiCraft\config.json');
    
    if not FileExists(ConfigFile) then
    begin
      ForceDirectories(ExpandConstant('{userappdata}\LemiCraft'));
      
      ConfigContent := TStringList.Create;
      try
        ConfigContent.Add('{');
        ConfigContent.Add('  "FirstRun": true,');
        ConfigContent.Add('  "InstalledVersion": "' + ExpandConstant('{#AppVersion}') + '"');
        ConfigContent.Add('}');
        
        ConfigContent.SaveToFile(ConfigFile);
      finally
        ConfigContent.Free;
      end;
    end;
  end;
end;

// ================================================
// ФУНКЦИЯ: Инициализация деинсталляции
// ================================================
function InitializeUninstall(): Boolean;
var
  UserDataDir: String;
  GameDir: String;
  Msg: String;
  MsgResult: Integer;
begin
  Result := True;
  
  UserDataDir := ExpandConstant('{userappdata}\LemiCraft');
  
  if not RegQueryStringValue(HKCU, 'Software\LemiCraft\Launcher', 'GamePath', GameDir) then
    GameDir := '';
  
  if DirExists(UserDataDir) or ((GameDir <> '') and DirExists(GameDir)) then
  begin
    Msg := 'Обнаружены пользовательские данные:' + #13#10;
    
    if DirExists(UserDataDir) then
      Msg := Msg + #13#10 + '• Настройки и кеш: ' + UserDataDir;
      
    if (GameDir <> '') and DirExists(GameDir) then
      Msg := Msg + #13#10 + '• Папка игры: ' + GameDir;
    
    Msg := Msg + #13#10 + #13#10 + 
           'Что вы хотите сделать?' + #13#10 + #13#10 +
           'Да — Удалить всё (настройки, моды, сохранения)' + #13#10 +
           'Нет — Оставить данные (можно будет использовать при переустановке)' + #13#10 +
           'Отмена — Прервать удаление';
    
    MsgResult := MsgBox(Msg, mbConfirmation, MB_YESNOCANCEL);
    
    case MsgResult of
      IDYES:
        begin
          if DirExists(UserDataDir) then
            DelTree(UserDataDir, True, True, True);
          if (GameDir <> '') and DirExists(GameDir) then
            DelTree(GameDir, True, True, True);
        end;
      IDNO:
        begin
          // Ничего не удаляем
        end;
      IDCANCEL:
        begin
          Result := False;
        end;
    end;
  end;
end;

// ================================================
// ПРОЦЕДУРА: Действия после деинсталляции
// ================================================
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\LemiCraft\Launcher');
    RegDeleteKeyIfEmpty(HKCU, 'Software\LemiCraft');
    
    MsgBox(
      'Спасибо за использование LemiCraft Launcher!' + #13#10 + #13#10 +
      'Будем рады видеть вас снова на сервере!' + #13#10 + #13#10 +
      'Discord: https://discord.gg/ybC6QM8WTM', 
      mbInformation, MB_OK);
  end;
end;

// ================================================
// End of Script
// ================================================
