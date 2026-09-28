# SSHの標準入力から要求を読み、検証機内だけでHTTPを送る
# Read the request from SSH stdin and send HTTP only inside the verification machine
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# ディスク・JSON・HTTPは外部境界なので、失敗をstderrと終了コードへ残す
# Disk, JSON and HTTP are external boundaries; preserve failures on stderr and in exit status
try {
    # C# の GameSystemPaths.GameSystemDirectory + RemoteExecAccessFile.DirectoryName/FileName と同じ場所
    # Matches the C# GameSystemPaths.GameSystemDirectory + RemoteExecAccessFile.DirectoryName/FileName
    $access = Get-Content -Raw "$env:APPDATA\.moorestech\RemoteExec\access.json" | ConvertFrom-Json
    $body = [Console]::In.ReadToEnd()
    $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "http://127.0.0.1:$($access.port)/api/remote-exec" -Headers @{'X-Remote-Exec-Token'=$access.token} -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) -MaximumRedirection 0
    [Console]::Out.Write($response.Content)
    if ($response.StatusCode -ne 200) {
        [Console]::Error.WriteLine("ERROR: HTTP $($response.StatusCode)")
        exit 1
    }
    if (($response.Content | ConvertFrom-Json).outcome -ne 'Succeeded') {
        [Console]::Error.WriteLine('ERROR: execution outcome was not Succeeded')
        exit 2
    }
} catch {
    [Console]::Error.WriteLine("ERROR: remote exec failed: $_")
    if ($_.ErrorDetails.Message) { [Console]::Out.Write($_.ErrorDetails.Message) }
    exit 1
}
