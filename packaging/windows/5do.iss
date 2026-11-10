; 5DO Windows installer (Inno Setup 6+).
;
; Build with packaging/windows/build-installer.ps1. The script passes BundleDir,
; AppVersion, Architecture, and the architecture compatibility directives. The
; installer is unsigned unless a sign tool is supplied with -SignCommand.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef BundleDir
  #define BundleDir "..\..\artifacts\bundle\win-x64"
#endif

#ifndef Architecture
  #define Architecture "x64"
#endif

#ifndef ArchitecturesAllowed
  #define ArchitecturesAllowed "x64compatible"
#endif

#ifndef ArchitecturesInstallIn64BitMode
  #define ArchitecturesInstallIn64BitMode "x64compatible"
#endif

[Setup]
AppId={{7F1F20B2-6D06-4C2B-9C9D-0C9B1D5C6E11}
AppName=5DO
AppVersion={#AppVersion}
AppVerName=5DO {#AppVersion}
AppPublisher=5DO
AppPublisherURL=https://github.com/purelogiccode/5DO
AppSupportURL=https://github.com/purelogiccode/5DO/issues
AppUpdatesURL=https://github.com/purelogiccode/5DO/releases
DefaultDirName={autopf}\5DO
DefaultGroupName=5DO
DisableProgramGroupPage=yes
OutputBaseFilename=5do-{#AppVersion}-win-{#Architecture}-setup
OutputDir=..\..\artifacts\installers
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed={#ArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#ArchitecturesInstallIn64BitMode}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ChangesAssociations=yes
CloseApplications=yes
RestartApplications=no
SetupIconFile=..\..\src\FiveDo.Frontend.Avalonia\icon2.ico
LicenseFile=..\..\LICENSE
UninstallDisplayIcon={app}\FiveDo.Frontend.Avalonia.exe
#ifdef SignToolName
SignTool={#SignToolName}
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "fileassoc"; Description: "Associate 3DO disc images (.iso, .cue, .chd, .bin, .m3u) with 5DO"; GroupDescription: "File associations:"

[Files]
Source: "{#BundleDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\5DO"; Filename: "{app}\FiveDo.Frontend.Avalonia.exe"
Name: "{group}\5DO CLI"; Filename: "{app}\FiveDo.Cli.exe"
Name: "{group}\Uninstall 5DO"; Filename: "{uninstallexe}"
Name: "{autodesktop}\5DO"; Filename: "{app}\FiveDo.Frontend.Avalonia.exe"; Tasks: desktopicon

[Registry]
; Per-user associations when the installer runs without elevation, machine-wide
; associations when the user elevates. HKA selects the correct hive.
Root: HKA; Subkey: "Software\Classes\.iso"; ValueType: string; ValueName: ""; ValueData: "5DO.Disc"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.cue"; ValueType: string; ValueName: ""; ValueData: "5DO.Disc"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.chd"; ValueType: string; ValueName: ""; ValueData: "5DO.Disc"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.bin"; ValueType: string; ValueName: ""; ValueData: "5DO.Disc"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.m3u"; ValueType: string; ValueName: ""; ValueData: "5DO.Playlist"; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\5DO.Disc"; ValueType: string; ValueName: ""; ValueData: "3DO Disc Image"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\5DO.Disc\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\FiveDo.Frontend.Avalonia.exe,0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\5DO.Disc\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FiveDo.Frontend.Avalonia.exe"" ""%1"""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\5DO.Playlist"; ValueType: string; ValueName: ""; ValueData: "3DO Disc Playlist"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\5DO.Playlist\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\FiveDo.Frontend.Avalonia.exe,0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\5DO.Playlist\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\FiveDo.Frontend.Avalonia.exe"" ""%1"""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\FiveDo.Frontend.Avalonia.exe\SupportedTypes"; ValueType: string; ValueName: ".iso"; ValueData: ""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\FiveDo.Frontend.Avalonia.exe\SupportedTypes"; ValueType: string; ValueName: ".cue"; ValueData: ""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\FiveDo.Frontend.Avalonia.exe\SupportedTypes"; ValueType: string; ValueName: ".chd"; ValueData: ""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\FiveDo.Frontend.Avalonia.exe\SupportedTypes"; ValueType: string; ValueName: ".bin"; ValueData: ""; Tasks: fileassoc

[Run]
Filename: "{app}\FiveDo.Frontend.Avalonia.exe"; Description: "Launch 5DO"; Flags: nowait postinstall skipifsilent
