# 1) 앱(Duru.exe) 빌드 → out\  2) 앱을 품은 설치 파일(Duru-Setup.exe) 빌드 → out\  3) 메신저·메일 전송용 zip
$d = $PSScriptRoot
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = Join-Path $d 'out'; New-Item -ItemType Directory -Force $out | Out-Null
$app = Join-Path $out 'Duru.exe'
$refs = '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:Microsoft.CSharp.dll', '/r:System.Core.dll'

# 인자를 문자열로 직접 만들어 넘긴다 (Windows PowerShell 5 와 PowerShell 7 이 따옴표를 다르게 처리하므로)
$res = { param($file, $name) "/resource:$file,$name" }
$appArgs = @('/nologo', '/target:winexe', '/codepage:65001', "/out:$app", "/win32icon:$d\app.ico",
  (& $res "$d\viewer.html" 'viewer.html'), (& $res "$d\marked.min.js" 'marked.min.js'),
  (& $res "$d\PretendardVariable.woff2" 'PretendardVariable.woff2'), (& $res "$d\icon.png" 'icon.png'),
  (& $res "$d\THIRD_PARTY_NOTICES.txt" 'notices.txt')) + $refs + @('/r:System.Web.Extensions.dll', "$d\Duru.cs", "$d\Setup.cs", "$d\Lang.cs")
& $csc @appArgs
if ($LASTEXITCODE) { exit $LASTEXITCODE }

$setup = Join-Path $out 'Duru-Setup.exe'
$setupArgs = @('/nologo', '/target:winexe', '/codepage:65001', "/out:$setup", "/win32icon:$d\app.ico",
  (& $res $app 'Duru.exe'), (& $res "$d\icon.png" 'icon.png')) + $refs + @("$d\Installer.cs", "$d\Setup.cs", "$d\Lang.cs")
& $csc @setupArgs
if ($LASTEXITCODE) { exit $LASTEXITCODE }

# 메신저(카톡 등)·메일은 exe 첨부를 막는 경우가 많아 zip 으로도 묶어 둔다 (라이선스 고지 포함)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = Join-Path $out 'Duru-Setup.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
$z = [IO.Compression.ZipFile]::Open($zip, 'Create', [Text.Encoding]::UTF8)
[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $setup, [IO.Path]::GetFileName($setup), 'Optimal')
[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, "$d\THIRD_PARTY_NOTICES.txt", 'THIRD_PARTY_NOTICES.txt', 'Optimal')
$z.Dispose()

# 다운로드 검증용 SHA-256
Get-ChildItem $out -Include 'Duru-Setup.exe', 'Duru-Setup.zip' -Recurse | ForEach-Object {
  (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower() + '  ' + $_.Name
} | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
exit 0
