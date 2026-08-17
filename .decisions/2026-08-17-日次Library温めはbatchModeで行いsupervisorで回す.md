決定: メインクローンの日次Library温めは Unity `-batchMode -quit` で行い、always-on supervisor の periodic(dispatch+nohup worker) で日次スケジュールする。

棄却案: HANDOFF記載どおり `uloop launch`→`uloop compile`→`uloop launch -q` でGUI Editorを立てる案（hourly の dev-server-reaper が「coding-agentに所有されないGUI Editor」を30分経過でSIGTERM→SIGKILLするため、温め中に刈られてLibraryが壊れる。回避には「ジョブ所有マーカー」を新設して reaper と unity-modal-watchdog を改修する必要があり、保護機構に穴を開ける） / LaunchAgentのStartCalendarIntervalで直接日次起動する案（最小だが管理方式が3系統に分かれている現状をさらに伸ばす。CLAUDE.mdの「新規サービスは原則supervisorへ」に反する）。

理由: `-batchMode` は dev-server-reaper と unity-modal-watchdog の両方が設計上無視する（前者は `-batchmode` 引数を見て候補から除外、後者は worker として skip）。したがって既存の保護機構を一切変更せずに干渉がゼロになる。実測でも rc=0・43秒・ライセンス接続正常・ArtifactDB更新を確認済み。

リンク: [[2026-08-17-worktreeはタスク毎使い捨てでメインのUnity起動は日次Library生成のみ]]、bd moorestech-qq7、CLAUDE.local.md
