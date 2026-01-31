[Setup]
AppName=Resaa Scanner Service
AppVersion=1.0
DefaultDirName={pf}\ResaaScanner
DefaultGroupName=Resaa Softwares
OutputDir=.
OutputBaseFilename=ResaaScannerSetup
Compression=lzma2
SolidCompression=yes

[Files]
Source: "ScannerService.Api\bin\Publish\*"; DestDir: "{app}"; Flags: recursesubdirs
Source: "ScannerService.TrayApp\bin\Publish\*"; DestDir: "{app}"; Flags: recursesubdirs
Source: ".env"; DestDir: "{app}"; Flags: confirmoverwrite

[Icons]
Name: "{group}\Scanner Service"; Filename: "{app}\ScannerService.TrayApp.exe"
Name: "{commonstartup}\Scanner Service"; Filename: "{app}\ScannerService.TrayApp.exe"

[Run]
Filename: "{app}\ScannerService.TrayApp.exe"; Description: "Launch Scanner Service"; Flags: nowait postinstall skipifsilent