#define MyAppName "ClearPDF"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ClearPDF"
#define MyAppExeName "ClearPDF.exe"
#define MyAppAssocName "PDF Document"
#define MyAppAssocExt ".pdf"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
AppId={{A7C3E5F1-9B24-4D8E-A1C2-ClearPDF0001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=C:\Users\Steve\Documents
OutputBaseFilename=ClearPDF-Setup
SetupIconFile=app-icon.ico
WizardImageFile=inno-wizard-164x314.bmp
WizardSmallImageFile=inno-icon-55.bmp
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "associate"; Description: "Set ClearPDF as default for .pdf files"; GroupDescription: "File associations:"; Flags: unchecked

[Files]
Source: "C:\ClearPDF-publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKLM; Subkey: "Software\Classes\ClearPDF.Document"; ValueType: string; ValueName: ""; ValueData: "PDF Document"; Flags: uninsdeletekey; Tasks: associate
Root: HKLM; Subkey: "Software\Classes\ClearPDF.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associate
Root: HKLM; Subkey: "Software\Classes\ClearPDF.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associate
Root: HKLM; Subkey: "Software\Classes\.pdf"; ValueType: string; ValueName: ""; ValueData: "ClearPDF.Document"; Flags: uninsdeletevalue; Tasks: associate
Root: HKLM; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "ClearPDF.Document"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
