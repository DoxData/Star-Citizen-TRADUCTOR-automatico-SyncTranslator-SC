[Setup]
AppName=SyncTranslator SC
AppVersion=1.0
DefaultDirName={pf}\SyncTranslator SC
DefaultGroupName=SyncTranslator SC
OutputDir=.
OutputBaseFilename=SyncTranslatorSC_Installer
SetupIconFile=icono.ico

[Files]
Source: "bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SyncTranslator SC"; Filename: "{app}\SyncTranslatorSC.exe"
Name: "{group}\Uninstall SyncTranslator SC"; Filename: "{uninstallexe}"
Name: "{commondesktop}\SyncTranslator SC"; Filename: "{app}\SyncTranslatorSC.exe"

[Run]
Filename: "{app}\SyncTranslatorSC.exe"; Description: "{cm:LaunchProgram,SyncTranslator SC}"; Flags: nowait postinstall skipifsilent
