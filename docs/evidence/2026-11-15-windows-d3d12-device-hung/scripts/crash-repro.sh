#!/usr/bin/env bash
# 指定コミットで、ワールドを読み込み定常後に place-player.cs で重い場所へ移すだけを行い、60秒後に Unity が生きているか・GPU エラーが出たかを見る
C="$1"; EXP="${2:-exp-none.cs}"; PLACE="${3:-place-player.cs}"; TAG="${4:-$C}"; ANALYZE="${5:-}"
B=/Users/juha/Documents/GitHub/moorestech/docs/performance/benchmark
L=/Users/juha/Documents/GitHub/moorestech/moorestech_client/PlaytestResults/scratch-local
P='G:\GitHub\moorestech\moorestech_client'; SN='C:\moorestech-perf\snippets'
remote() { ssh -o ConnectTimeout=10 windows-desktop "$@"; }
run() { remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $P" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
print(d.get("Result","") if d.get("Success") else "FAIL "+t[:300])'; }
remote "Get-Process Unity,UnityBugReporter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 5; Remove-Item G:\GitHub\moorestech\moorestech_client\Temp\UnityLockfile, G:\GitHub\moorestech\moorestech_client\Temp\__Backupscenes -Recurse -Force -ErrorAction SilentlyContinue; cd G:\GitHub\moorestech; git checkout -q -- .; git checkout -q $C; git log --oneline -1; Start-ScheduledTask -TaskName moorestech-unity"
for i in $(seq 1 60); do r=$(remote "uloop compile --project-path $P" 2>&1); echo "$r" | grep -q '"Success": true' && break; sleep 10; done
echo "compile: $(echo "$r" | grep -c '"Success": true')"
scp -q "$B"/snippets/*.cs "$L"/exp-*.cs "$L"/teleport-only.cs "$L"/analyze-view.cs "windows-desktop:C:/moorestech-perf/snippets/"
run open-mainmenu.cs
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $P" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs)"; case "$r" in started*) break ;; esac; sleep 3; done
echo "enter: $r"
prev=-1
for _ in $(seq 1 120); do sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then break; fi
  prev="$cur"
done
sleep 20
echo "exp: $(run $EXP)"
sleep 5
echo "place: $(run $PLACE)"
sleep 45
[ -n "$ANALYZE" ] && run $ANALYZE > "$L/analyze-$TAG.txt"
echo "alive: $(remote 'Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -ne "" } | Select-Object -ExpandProperty MainWindowTitle')"
echo "gpu-errors: $(remote 'Select-String -Path $env:LOCALAPPDATA\Unity\Editor\Editor.log -Pattern "Device failed|Unrecoverable D3D12" | Measure-Object | Select-Object -ExpandProperty Count')"
remote "Remove-Item C:\\moorestech-perf\\screen.png -ErrorAction SilentlyContinue; Start-ScheduledTask -TaskName moorestech-screenshot"; sleep 6
scp -q windows-desktop:C:/moorestech-perf/screen.png "$L/crash-repro-$TAG.png" && echo "screenshot: $L/crash-repro-$TAG.png"
run cleanup.cs >/dev/null; remote "uloop control-play-mode --action Stop --project-path $P" >/dev/null 2>&1
