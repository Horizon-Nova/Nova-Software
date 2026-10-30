; HelixStudio 與 HelixStudio Demo 共用的安裝程式：值都從 package\products\*.json 來，由 dev\Release 帶參數編譯
;   AppId          產品的 AppId；定了就不換，換了 Windows 會把新版當成另一套軟體
;   AppName        「新增或移除程式」與開始功能表上的名字
;   InstallFolder  裝在 Program Files 底下的資料夾名
;   DataFolder     ProgramData 底下的資料夾名，也是開機自動啟動在登錄檔裡那一項的名字
;   Service        資料庫服務的名字
;   Port           資料庫只聽本機的這個埠
;   Icon           安裝程式的圖示
;   Legal          使用者授權合約所在的資料夾
;   Arch           x64 或 x86
;   MinOs          最低 Windows 版本：10.0 或 6.1sp1
;   Dist           這一版平台發佈出來的資料夾（.NET 執行環境已經包在裡面，不靠這台電腦裝的 .NET）
;   Pg             這一版帶的 PostgreSQL：...\{PostgreSQL 版本}\pgsql（bin、lib、share）
;   Redists        這一版要一併安裝的 VC++ 執行元件所在的資料夾，裡面每一個都會裝
;   NeedKb         Windows 7 要先有的系統更新（Win7 上的 .NET 6 要 KB3063858）；沒有就是空的
;   Tools          跟安裝檔一起來的工具套件所在的資料夾；沒有就是空的
;   AfterSql       資料表建好後再執行的一段 SQL；沒有就是空的
;   DatabaseVersion 資料庫結構是哪一版定的；這台電腦記下的跟它不一樣，就刪掉舊的資料庫照 init 重建
;   Database       資料表腳本的資料夾（init：討論好的結構，安裝時照它建表）
; 安裝前先顯示使用者授權合約，按「我同意」才能繼續

#define Exe AddBackslash(Dist) + "HelixStudio.exe"
; 資料庫的資料只有同一個 PostgreSQL 版本讀得懂：資料放在以它命名的資料夾，換版本就是一個新的空資料庫，舊的留著不動
#define PgBuild ExtractFileName(ExtractFileDir(Pg))

[Setup]
AppId={{{#AppId}}
AppName={#AppName}
AppVersion={#GetStringFileInfo(Exe, "ProductVersion")}
AppPublisher=曜域科技 Vaeron
AppCopyright=Copyright © 2026 曜域科技 Vaeron
DefaultDirName={autopf}\{#InstallFolder}
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\HelixStudio.exe
SetupIconFile={#Icon}
LicenseFile={#Legal}\EULA.txt
MinVersion={#MinOs}
PrivilegesRequired=admin
#if Arch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "cht"; MessagesFile: "ChineseTraditional.isl"

[Dirs]
; 安裝檔寫給平台的連線設定放這裡；平台第一次開起來把它鎖進操作員自己的保險箱並刪掉明文，所以操作員要能改這個資料夾
Name: "{commonappdata}\{#DataFolder}"; Permissions: users-modify

[Files]
Source: "{#Dist}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "{#Pg}\*"; DestDir: "{app}\pgsql"; Flags: ignoreversion recursesubdirs
Source: "{#Database}\init\*.sql"; DestDir: "{app}\database\init"; Flags: ignoreversion
Source: "{#Redists}\*.exe"; DestDir: "{tmp}\redist"; Flags: deleteafterinstall
#if Tools != ""
Source: "{#Tools}\*.stpkg"; DestDir: "{app}\tools"; Flags: ignoreversion
#endif

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\HelixStudio.exe"

; 開機時自動啟動是平台照設定寫的；移除時一併拿掉，不留一個指向已刪掉程式的啟動項目
[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#DataFolder}"; ValueType: none; Flags: uninsdeletevalue dontcreatekey

; 資料庫服務剛停時 PostgreSQL 的檔案可能還被鎖著一下，移除時整個資料夾再清一次，不留空殼
[UninstallDelete]
Type: filesandordirs; Name: "{app}\pgsql"

[Code]
const
  AppId = '{#AppId}';
  DataFolder = '{#DataFolder}';
  Service = '{#Service}';
  Port = '{#Port}';
  NeedKb = '{#NeedKb}';
  PgBuild = '{#PgBuild}';
  AfterSql = '{#StringChange(AfterSql, "'", "''")}';
  DatabaseVersion = '{#DatabaseVersion}';

{ 這個 PostgreSQL 版本的資料、密碼、資料庫結構版本：只給系統管理員與 SYSTEM；移除平台時留著，重裝接著用 }
function DbDir: String;
begin
  Result := ExpandConstant('{commonappdata}\') + DataFolder + '\db\' + PgBuild;
end;

function DataDir: String;
begin
  Result := DbDir + '\pgdata';
end;

function KeyFile: String;
begin
  Result := DbDir + '\db.key';
end;

{ 這台電腦的資料表是照哪一版的結構建的 }
function SchemaFile: String;
begin
  Result := DbDir + '\database.txt';
end;

function Bin(Name: String): String;
begin
  Result := ExpandConstant('{app}\pgsql\bin\') + Name;
end;

{ 執行一支程式並等它結束；回傳結束碼，沒跑起來回傳 -1。參數可能帶密碼，紀錄只寫程式名稱 }
function Run(FileName, Params: String): Integer;
var
  Code: Integer;
begin
  if Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, Code) then
    Result := Code
  else
    Result := -1;
  Log(ExtractFileName(FileName) + ' 結束碼 ' + IntToStr(Result));
end;

{ 這台電腦有沒有裝某一個 Windows 更新 }
function HasUpdate(Kb: String): Boolean;
var
  Names: TArrayOfString;
  I, Root: Integer;
begin
  Result := False;
  if IsWin64 then Root := HKLM64 else Root := HKLM;
  if RegGetSubkeyNames(Root, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Pos(Uppercase(Kb), Uppercase(Names[I])) > 0 then
        Result := True;
end;

{ 這一版要的 Windows 更新只有 Windows 7（6.1）需要；這台是 Windows 7 又沒裝，平台裝了也跑不起來，說清楚要先裝哪一個 }
function InitializeSetup: Boolean;
begin
  Result := (NeedKb = '') or ((GetWindowsVersion shr 16) <> $0601) or HasUpdate(NeedKb);
  if not Result then
    SuppressibleMsgBox('這台電腦缺少 Windows 更新 ' + NeedKb + '。' + #13#10#13#10 +
      '{#AppName} 在 Windows 7 上要有這個更新才能執行。請先從 Windows Update 或微軟下載中心安裝 ' + NeedKb + '，再執行一次這個安裝檔。',
      mbCriticalError, MB_OK, IDOK);
end;

{ 這台電腦以另一個位元裝著的同一套軟體（x64 改裝 x32 或反過來）的移除程式；沒有就是空字串 }
function OtherUninstaller: String;
var
  Root: Integer;
  Value: String;
begin
  Result := '';
  if Is64BitInstallMode then Root := HKLM32 else Root := HKLM64;
  if IsWin64 and RegQueryStringValue(Root, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{' + AppId + '}_is1', 'UninstallString', Value) then
    Result := RemoveQuotes(Value);
end;

{ 換檔之前：先停資料庫服務，PostgreSQL 的檔案才換得掉；另一個位元裝著的那一份先靜默移除，不留兩份（它的資料庫資料照樣留著） }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  Run(ExpandConstant('{sys}\net.exe'), 'stop ' + Service);
  if OtherUninstaller <> '' then
    Run(OtherUninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART');
end;

{ 一組隨機的英數字密碼 }
function NewPassword: String;
var
  Chars: String;
  I: Integer;
begin
  Chars := 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789';
  Result := '';
  for I := 1 to 32 do
    Result := Result + Chars[Random(Length(Chars)) + 1];
end;

{ 用 psql 對資料庫執行一段 SQL 或一個檔；回傳結束碼。
  Windows 版 psql 不讀連線字串後面的參數：參數放前面，連線字串用 -d 帶；-w 不問密碼，連不上直接回錯。
  SQL 檔是 UTF-8，psql 在主控台會照主控台的編碼（繁中是 Big5）送字，先講明是 UTF-8 }
function Psql(Password, Database, What: String): Integer;
begin
  Result := Run(Bin('psql.exe'), '-v ON_ERROR_STOP=1 -q -w -c "set client_encoding to ''UTF8''" ' + What + ' -d "host=localhost port=' + Port + ' user=helix password=' + Password + ' dbname=' + Database + '"');
end;

{ 一個資料夾裡的每一個檔，照檔名順序 }
function FilesIn(Folder, Pattern: String): TArrayOfString;
var
  Found: TFindRec;
  Names: TStringList;
  I: Integer;
begin
  Names := TStringList.Create;
  Names.Sorted := True;
  if FindFirst(AddBackslash(Folder) + Pattern, Found) then
  begin
    repeat
      Names.Add(Found.Name);
    until not FindNext(Found);
    FindClose(Found);
  end;
  SetArrayLength(Result, Names.Count);
  for I := 0 to Names.Count - 1 do
    Result[I] := Names[I];
  Names.Free;
end;

function ReadText(FileName: String): String;
var
  Raw: AnsiString;
begin
  Raw := '';
  LoadStringFromFile(FileName, Raw);
  Result := Trim(String(Raw));
end;

procedure Fail(What: String);
begin
  SuppressibleMsgBox(What + #13#10#13#10 + '安裝紀錄在 ' + ExpandConstant('{log}') + '。再執行一次這個安裝檔可以重試。', mbError, MB_OK, IDOK);
end;

{ 檔案換好之後依序：VC++ 執行元件、資料庫、資料庫服務、資料表、平台的連線設定 }
procedure CurStepChanged(CurStep: TSetupStep);
var
  Password: String;
  I: Integer;
  Fresh: Boolean;
  Files: TArrayOfString;
begin
  if CurStep <> ssPostInstall then Exit;

  { VC++ 執行元件：PostgreSQL 與 .NET 要用；這一版帶的每一個都跑一次靜默安裝，被移除或壞掉的會補回來 }
  WizardForm.StatusLabel.Caption := '安裝 VC++ 執行元件…';
  Files := FilesIn(ExpandConstant('{tmp}\redist'), '*.exe');
  for I := 0 to GetArrayLength(Files) - 1 do
    Run(ExpandConstant('{tmp}\redist\') + Files[I], '/install /quiet /norestart');

  { 這個 PostgreSQL 版本在這台電腦還沒有資料庫：建一個，隨機密碼，只聽本機 }
  Fresh := not FileExists(DataDir + '\PG_VERSION');
  if Fresh then
  begin
    WizardForm.StatusLabel.Caption := '建立資料庫…';
    ForceDirectories(DbDir);
    Password := NewPassword;
    SaveStringToFile(ExpandConstant('{tmp}\pw.txt'), Password, False);
    I := Run(Bin('initdb.exe'), '-D "' + DataDir + '" -U helix --pwfile="' + ExpandConstant('{tmp}\pw.txt') + '" -E UTF8 --locale=C -A md5');
    DeleteFile(ExpandConstant('{tmp}\pw.txt'));
    if I <> 0 then
    begin
      Fail('建立資料庫失敗。');
      Exit;
    end;
    SaveStringToFile(DataDir + '\postgresql.conf', #13#10 + 'port = ' + Port + #13#10 + 'listen_addresses = ''localhost''' + #13#10, True);
    SaveStringToFile(KeyFile, Password, False);
    { initdb 用降權的身分跑，要在它建完之後才收權限；資料庫服務用 SYSTEM 執行。只改資料夾，裡面的檔案照繼承跟著換 }
    Run(ExpandConstant('{sys}\icacls.exe'), '"' + DbDir + '" /inheritance:r /grant:r *S-1-5-32-544:(OI)(CI)F *S-1-5-18:(OI)(CI)F /Q');
  end;
  Password := ReadText(KeyFile);

  { 資料庫服務：每次安裝都照這一版的 PostgreSQL 與資料夾重新註冊（被刪掉、換了版本都接得回來），開機自動啟動，停掉了自己重啟 }
  WizardForm.StatusLabel.Caption := '註冊資料庫服務…';
  Run(Bin('pg_ctl.exe'), 'unregister -N ' + Service);
  if Run(Bin('pg_ctl.exe'), 'register -N ' + Service + ' -D "' + DataDir + '" -S auto -w') <> 0 then
  begin
    Fail('註冊資料庫服務失敗。');
    Exit;
  end;
  Run(ExpandConstant('{sys}\sc.exe'), 'failure ' + Service + ' reset= 86400 actions= restart/5000/restart/5000/restart/5000');
  WizardForm.StatusLabel.Caption := '啟動資料庫服務…';
  Run(ExpandConstant('{sys}\net.exe'), 'start ' + Service);
  if Run(Bin('pg_isready.exe'), '-h localhost -p ' + Port) <> 0 then
  begin
    Fail('資料庫服務沒有啟動。');
    Exit;
  end;

  { 資料表：這台電腦記下的資料庫結構版本跟這一版不一樣（新建的庫、2.0.0 沒記過、這一版改了結構），刪掉舊的資料庫照 init 重建；一樣就照原樣留著 }
  if ReadText(SchemaFile) <> DatabaseVersion then
  begin
    WizardForm.StatusLabel.Caption := '建立資料表…';
    Psql(Password, 'postgres', '-c "select pg_terminate_backend(pid) from pg_stat_activity where datname = ''helix_studio''"');
    if Psql(Password, 'postgres', '-c "drop database if exists helix_studio"') <> 0 then
    begin
      Fail('刪除舊的資料庫 helix_studio 失敗。');
      Exit;
    end;
    if Psql(Password, 'postgres', '-c "create database helix_studio"') <> 0 then
    begin
      Fail('建立資料庫 helix_studio 失敗。');
      Exit;
    end;
    Files := FilesIn(ExpandConstant('{app}\database\init'), '*.sql');
    for I := 0 to GetArrayLength(Files) - 1 do
      if Psql(Password, 'helix_studio', '-f "' + ExpandConstant('{app}\database\init\') + Files[I] + '"') <> 0 then
      begin
        Fail('建立資料表失敗：' + Files[I] + '。');
        Exit;
      end;
    SaveStringToFile(SchemaFile, DatabaseVersion, False);
  end;

  { 這個產品自己要的那一段 SQL（例如 Demo 拿掉它沒有的設定） }
  if AfterSql <> '' then
    if Psql(Password, 'helix_studio', '-c "' + AfterSql + '"') <> 0 then
    begin
      Fail('調整資料表失敗。');
      Exit;
    end;

  { 平台的連線設定：每次安裝都寫一份；平台開起來鎖進操作員的保險箱並刪掉明文（另一個 Windows 帳號要用，再跑一次安裝檔） }
  SaveStringToFile(ExpandConstant('{commonappdata}\') + DataFolder + '\appsettings.json',
    '{' + #13#10 +
    '  "ConnectionStrings": {' + #13#10 +
    '    "HelixStudio": "Host=localhost;Port=' + Port + ';Database=helix_studio;Username=helix;Password=' + Password + ';Timeout=5"' + #13#10 +
    '  }' + #13#10 + '}' + #13#10, False);
end;

{ 安裝結束（裝完、被取消、換檔失敗都一樣）：換檔前停掉的資料庫服務啟動回來；服務還在跑就什麼都不做 }
procedure DeinitializeSetup;
begin
  Run(ExpandConstant('{sys}\net.exe'), 'start ' + Service);
end;

{ 移除平台：停掉並移除資料庫服務；資料庫的資料與密碼留著，重裝接著用 }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usUninstall then Exit;
  Run(ExpandConstant('{sys}\net.exe'), 'stop ' + Service);
  Run(ExpandConstant('{app}\pgsql\bin\pg_ctl.exe'), 'unregister -N ' + Service);
end;
