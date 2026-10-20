# 빌드 중 실행 파일 바꾸기 (UiNametag.csproj가 부름)
# pre : 이 출력 폴더의 picker(주황 아이콘)를 끄고 기억, 잠긴 exe/dll은 이름만 바꿔 비킨다(MCP는 안 끊김)
# post: pre에서 꺼 둔 picker를 새 버전으로 다시 켠다
param([string]$Out, [string]$Phase)
$Out = (Resolve-Path $Out.Trim()).Path.TrimEnd('\')
$exe = Join-Path $Out 'UiNametag.exe'
$flag = Join-Path $Out 'restart.flag'

if ($Phase -eq 'pre') {
    Get-ChildItem $Out -Filter '*.old-*' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue }
    $pick = Get-CimInstance Win32_Process -Filter "Name='UiNametag.exe'" |
        Where-Object { $_.ExecutablePath -eq $exe -and $_.CommandLine -notmatch '\s(mcp|selftest|probe|build-catalog)' }
    if ($pick) { New-Item $flag -ItemType File -Force | Out-Null; $pick | ForEach-Object { Stop-Process -Id $_.ProcessId -Force } ; Start-Sleep -Milliseconds 500 }
    $tick = [DateTime]::Now.Ticks
    foreach ($f in 'UiNametag.exe', 'UiNametag.dll') {
        $p = Join-Path $Out $f
        if (Test-Path $p) { try { Rename-Item $p "$f.old-$tick" -ErrorAction Stop } catch { } }
    }
}
elseif ($Phase -eq 'post' -and (Test-Path $flag)) {
    Remove-Item $flag -Force
    Start-Process $exe
    Write-Host "UiNametag 새 버전으로 다시 켬"
}
