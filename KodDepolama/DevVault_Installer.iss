[Setup]
; App Information
AppName=DevVault
AppVersion=2.0.0
AppPublisher=Alperen Software
AppPublisherURL=https://github.com/alperen
DefaultDirName={autopf}\DevVault
DefaultGroupName=DevVault
DisableProgramGroupPage=yes
; Output setup executable name
OutputBaseFilename=DevVault_Setup
Compression=lzma
SolidCompression=yes
; Architecture
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; IMPORTANT: Change the Source path if you compile as Release instead of Debug.
Source: "bin\Debug\net10.0-windows\DevSnippetManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net10.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "bin\Debug\net10.0-windows\MonacoEditor.html"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Debug\net10.0-windows\DevSnippetManager.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\DevVault"; Filename: "{app}\DevSnippetManager.exe"
Name: "{autodesktop}\DevVault"; Filename: "{app}\DevSnippetManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DevSnippetManager.exe"; Description: "{cm:LaunchProgram,DevVault}"; Flags: nowait postinstall skipifsilent
