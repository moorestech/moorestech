# run-skill-live-trial workflow (物理契約 — 全チェックが [x] になるまで trial 終了禁止)

- [x] **Phase 0**: task.md Write (契約 5 項目) / WORK_DIR (絶対パス)・SESSION 確定 / 本 checklist を $WORK_DIR/workflow.md にコピー
- [x] **Phase 1**: boot READY (MODEL / SESSION_ID を記録。proof trial は MODEL 必須。BOOT_FAIL/TIMEOUT なら capture tail を記録して中断)
- [x] **Phase 2**: send 着手確認 (SENT)
- [ ] **Phase 3**: poll 終端 (DONE / GATE→応答→再poll / STALL→分岐表 / HARD_CAP)。終端 exit と nudge_count・gate 応答回数を記録
- [ ] **Phase 4**: 回収 3 点セット — out/ 保全 + pane.txt + transcript.jsonl (+ git status diff 記録) + model 検証 jq (requested/actual)
- [ ] **Phase 5**: fresh evaluator (goal 適合 0-100 / 合否 / 未達点)
- [ ] **Phase 6**: report.md Write (必須 field 表) + tmux kill-session
