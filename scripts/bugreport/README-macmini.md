# バグ報告の自動修正ラン（Mac mini側）

- 前提: `~/hermes-agent/data/repos/{moorestech,moorestech_logs,moorestech_master}` が存在し、`claude`・`gh`・`uloop`・`ffmpeg` が PATH にある
- 登録: always-on supervisor の periodic に `bash ~/hermes-agent/data/repos/moorestech/scripts/bugreport/inbox-poller.sh` を60秒周期で追加する（`.decisions/2026-08-14-独立レビュー無人化はsupervisor素pollerを起点にする.md` の poller と同じ置き方。`dev-server-reaper` の対象になるか R11 で確認）
  - **この登録（`services.json` への追記）は人手で行う。** 本番の supervisor はループ毎に `services.json` を再読込するため、追記した瞬間に常駐が始まる。実装・検証の段階では追記しない
  - periodic はメインループ内で同期実行されるため、`timeout_seconds` を必ず付ける（付けないと1ランが他の全サービスを止める）
- 手動実行: `bash scripts/bugreport/inbox-poller.sh`。ロック `$TMPDIR/moorestech-bugreport-poller.lock`
- 取りこぼさない・二度処理しない: `READY` が無い箱と運搬中の `<id>.partial` は掴まない。`runs/<id>` が既にある箱は理由をログして `inbox/.duplicate/<id>` へ隔離し、次の候補へ進む（入れ子破損も後続の恒久停止も作らない。隔離箱は人が見て捨てるか改名する）
- 実行制御の正本は SHA 固定の canon worktree: `canon_setup.py`（`pr-independent-review` と同じピン `skills-canon-<sha8>`）が用意した木を cwd にして `claude` を起こす。スキル本文・同梱スクリプト・hook はそこから読み、ゲームコードを直す先だけが `--add-dir` の `bugfix-<id>` worktree。canon が無い／canon にスキルが入っていない（master 未マージ）ときはランを起こさず `fix-result.json` に failure を書く
- 成果物: `moorestech_logs/harness/bug-report/runs/<id>/`（`fix-result.json`・`claude.out.json`・`observe/`・`replay-check.json`・`packets.jsonl`）
- worktree は `~/hermes-agent/data/repos/moorestech-worktrees/bugfix-<id>`。完了後も消さない（裁定 2026-08-17）。既に同名があれば `prepare-run.sh` は削除せず中断する
