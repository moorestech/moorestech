#!/usr/bin/env bash
# 使い方: session.sh <meshDeformation 0/1/2> <観察秒数> <tag>
# Unity を起動 → 設定を切り替え → ワールド読み込み → 定常待ち → 重い場所へ → 10秒ごとに応答を見る
MD="$1"; OBS="${2:-120}"; TAG="$3"; EXP="${4:-exp-none.cs}"
S=/private/tmp/claude-501/-Users-juha-Documents-GitHub-moorestech/67013790-36a7-477d-b048-9c04802732aa/scratchpad
P='G:\GitHub\moorestech\moorestech_client'; SN='C:\moorestech-perf\snippets'
remote() { ssh -o ConnectTimeout=10 windows-desktop "$@"; }
run() { perl -e 'alarm shift; exec @ARGV' ${2:-60} ssh -o ConnectTimeout=10 windows-desktop "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $P" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
try:
  d=json.loads(t[s:]) if s>=0 else {}
  print(d.get("Result","") if d.get("Success") else "FAIL "+t[:300])
except Exception: print("NORESPONSE "+t[:200])'; }
ts() { date +%H:%M:%S; }
scp -q $S/snip/*.cs windows-desktop:C:/moorestech-perf/snippets/
remote "Set-Content -Path C:\moorestech-perf\mesh-deformation.txt -Value $MD"
remote "Get-Process Unity,UnityBugReporter -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 5; Remove-Item $P\Temp\UnityLockfile, $P\Temp\__Backupscenes -Recurse -Force -ErrorAction SilentlyContinue; Start-ScheduledTask -TaskName ${UTASK:-moorestech-unity}"
for i in $(seq 1 60); do r=$(remote "uloop compile --project-path $P" 2>&1); echo "$r" | grep -q '"Success": true' && break; sleep 10; done
echo "$(ts) [$TAG] compile ok=$(echo "$r" | grep -c '"Success": true')"
echo "$(ts) open: $(run open-mainmenu.cs)"
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
echo "$(ts) $(run set-mesh-deformation.cs)"
remote "uloop control-play-mode --action Play --project-path $P" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs)"; case "$r" in started*) break ;; esac; sleep 3; done
echo "$(ts) enter: $r"
prev=-1
for _ in $(seq 1 60); do sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then break; fi
  prev="$cur"
done
echo "$(ts) steady renderers=$cur"
echo "$(ts) vfx: $(run vfx-stats.cs)"
echo "$(ts) exp: $(run $EXP)"
echo "$(ts) place: $(run place-player.cs)"
sleep 5; echo "$(ts) vfx: $(run vfx-stats.cs)"
t0=$(date +%s); dead=0
while [ $(( $(date +%s) - t0 )) -lt "$OBS" ]; do sleep 10
  [ "$EXP" != exp-none.cs ] && run $EXP >/dev/null
  a="$(run skin-status.cs 25)"; echo "$(ts) +$(( $(date +%s) - t0 ))s: $a"
  case "$a" in focused=*) ;; *) dead=1; break ;; esac
done
echo "$(ts) gpu-errors: $(remote 'Select-String -Path $env:LOCALAPPDATA\Unity\Editor\Editor.log -Pattern "Device failed|Unrecoverable D3D12" | Measure-Object | Select-Object -ExpandProperty Count')"
remote "Copy-Item \$env:LOCALAPPDATA\Unity\Editor\Editor.log C:\moorestech-perf\editor-$TAG.log -Force"
if [ $dead = 0 ]; then remote "uloop control-play-mode --action Stop --project-path $P" >/dev/null 2>&1; fi
echo "$(ts) [$TAG] done dead=$dead"
