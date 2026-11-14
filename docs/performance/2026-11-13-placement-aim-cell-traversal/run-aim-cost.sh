#!/usr/bin/env bash
# 照準1本の最悪時間（placement-aim-trace-cost.cs）と、実プレイに近い照準の分布・索引への登録時間（placement-aim-realistic-cost.cs）を続けて測る
HOST=windows-desktop; PROJECT='G:\GitHub\moorestech\moorestech_client'; SN='C:\moorestech-perf\snippets'
SSH_OPTS=(-o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3)
remote() { ssh "${SSH_OPTS[@]}" "$HOST" "$@"; }
run() { remote "uloop execute-dynamic-code --code-file $SN\\$1 --project-path $PROJECT" 2>&1 | python3 -c '
import json,sys
t=sys.stdin.read(); s=t.find("{")
d=json.loads(t[s:]) if s>=0 else {}
print(d.get("Result","") if d.get("Success") else "FAIL "+t[:1500])'; }
teardown() { run cleanup.cs >/dev/null || true; remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true; }
trap teardown EXIT
scp -q "${SSH_OPTS[@]}" /Users/juha/Documents/GitHub/moorestech/docs/performance/benchmark/snippets/*.cs "$HOST:C:/moorestech-perf/snippets/"
remote "uloop control-play-mode --action Stop --project-path $PROJECT" >/dev/null 2>&1 || true
remote "\$b='C:\\moorestech-perf\\baselines\\2026-10-22-world_1'; \$w='C:\\moorestech-perf\\work\\current'; if (Test-Path \$w) { Remove-Item \$w -Recurse -Force }; New-Item -ItemType Directory -Force -Path \$w | Out-Null; Copy-Item \"\$b\\*\" \$w -Force"
remote "uloop control-play-mode --action Play --project-path $PROJECT" >/dev/null
for _ in $(seq 1 40); do r="$(run enter-world.cs)"; case "$r" in started*) break ;; esac; sleep 3; done
prev=-1
for _ in $(seq 1 120); do sleep 5
  cur="$(run scene-stats.cs | python3 -c 'import json,sys
try: print(json.load(sys.stdin)["renderers"])
except Exception: print(-1)')"
  if [ "$cur" -gt 1000 ] && [ "$prev" -gt 0 ] && [ $(( (cur > prev ? cur - prev : prev - cur) * 1000 )) -le "$cur" ]; then break; fi
  prev="$cur"
done
sleep 20
run placement-aim-trace-cost.cs
run placement-aim-realistic-cost.cs
