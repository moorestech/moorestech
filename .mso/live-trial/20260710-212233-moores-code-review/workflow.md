# run-skill-live-trial workflow (物理契約 — 全チェックが [x] になるまで trial 終了禁止)

- [x] **Phase 0**: task.md Write (契約 5 項目) / WORK_DIR (絶対パス)・SESSION 確定 / 本 checklist を $WORK_DIR/workflow.md にコピー
- [x] **Phase 1**: boot READY (MODEL:default, SESSION_ID:f4f8d6e2-ffbf-46f6-85d3-4e5bb15d1d62, 3s) (MODEL / SESSION_ID を記録。proof trial は MODEL 必須。BOOT_FAIL/TIMEOUT なら capture tail を記録して中断)
- [x] **Phase 2**: send 着手確認 (SENT)
- [x] **Phase 3**: poll 終端 exit 2 STALL（誤検知: teammate重複配信でjsonl BUSY凍結。分岐表でマーカー出現+idle+DONE報告を確認し完走判定）。nudge 0 / gate応答 0
- [x] **Phase 4**: 回収 3 点セット完了 + actual_model=claude-fable-5 + worktree副作用なし
- [x] **Phase 5**: fresh evaluator 90/100 合格（recall 6/6・ハルシネーション0・stale破棄15件妥当）
- [x] **Phase 6**: report.md Write + tmux kill-session
