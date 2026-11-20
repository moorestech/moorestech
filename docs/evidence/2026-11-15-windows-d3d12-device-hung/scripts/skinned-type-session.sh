#!/usr/bin/env bash
# 1回の読み込みで：ブロックの描画を切る → 重い場所へ → 映る種類を集計 → 種類を1つずつ戻し、戻すたびに 12秒待って Unity が応答するかを見る
L=/Users/juha/Documents/GitHub/moorestech/moorestech_client/PlaytestResults/scratch-local
B=/Users/juha/Documents/GitHub/moorestech/docs/performance/benchmark
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
scp -q "$B"/snippets/*.cs "$L"/exp-*.cs "$L"/analyze-view.cs "$L"/enable-next.cs "$L"/set-order-crusher-first.cs "$L"/enable-skinned.cs "$L"/skinned-stats.cs "$L"/enable-skinned-next.cs "windows-desktop:C:/moorestech-perf/snippets/"
echo "$(ts) open: $(run open-mainmenu.cs)"
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
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
echo "$(ts) exp: $(run exp-noskinned.cs)"
echo "$(ts) place: $(run place-player.cs)"
sleep 3
echo "$(ts) stats:"; run skinned-stats.cs
for i in 1 2 3 4 5 6; do
  r="$(run enable-skinned-next.cs)"; echo "$(ts) $r"
  case "$r" in END*|FAIL*|NORESPONSE*) break ;; esac
  sleep 15; a="$(run focus-status.cs 20)"; echo "$(ts) status: $a"
  case "$a" in focused=*) ;; *) echo "$(ts) CRASH after: $r"; break ;; esac
done
echo "$(ts) gpu-errors: $(remote 'Select-String -Path $env:LOCALAPPDATA\Unity\Editor\Editor.log -Pattern "Device failed|Unrecoverable D3D12" | Measure-Object | Select-Object -ExpandProperty Count')"
