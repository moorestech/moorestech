# live-trial report: Trial 1 — Opus L1（手順追従）

## 対象
- **skill**: unity-playmode-recorded-playtest
- **args**（task.md 引用）: 「tools/playtest/scenarios/belt-line-via-ui.cs を実行して結果を報告して」
- **評価コンテキスト**: プレイテストスキルのモデル別精度評価（docs/superpowers/plans/2026-07-07-playtest-skill-model-eval-handoff.md）Level 1

## model 検証（機械 gate）
- requested_model: `claude-opus-4-8`
- actual_model: `claude-opus-4-8`（transcript jq、単一値）→ **一致 ✅**

## timeline
- boot: READY 3s（SESSION_ID: e4b8ce97-c5ef-4d54-8b2a-ceb36b64eac9）
- poll: DONE 345s（exit 0、via jsonl、chunk 1 で終端）
- wall-clock: 約6分

## 介入
- nudge_count: **0** / gate 応答: **0**（完全自走）

## 成果物
- 完了マーカー: `out/status.json` — `{"status": "PASS", ..., "asserts_passed": 8, "asserts_total": 8, "attempts": 2}`
- result.json: `moorestech_client/PlaytestResults/20260707_153809/belt-line-via-ui/result.json`（Success:true, 8/8）
- 録画: recording.mp4（20.17s / 2.0MB、ffprobe 検証済み）+ スクショ4枚
- transcript: `transcript.jsonl`（743KB）/ pane: `pane.txt`
- git 副作用: `.moorestech-external-revisions.json` M のみ（Editor 自動書き換え、ハーネス側で revert 済み）

## goal 判定（fresh evaluator）
- **goal適合: 90 / 合格**
- 成功条件4つ（動画生成・内部state・スクショUI要素・実プレイ視点）すべて一次情報で達成
- 手順遵守: 初回 run-scenario.sh が boot 失敗（PlayMode突入ドメインリロードが UnityMcpSettings.json を .bak 化する未文書の環境race）→ troubleshooting.md §4 の正規remedy（.bak復元）+ 文書化部品の妥当な即興（手動 boot→ready→inject 再構成）で完走。分類 (b)
- 誤ルート: 禁じ手0（simulate系0・共有masterHEAD 0・ポート見落とし0）、軽微な固定sleep 1回のみ
- attempts 実測: 2（自称と一致）
- 切り分け精度: 正確（環境要因と明示分類、観測→対処の順を遵守）
- 未達点: one-shot happy path 非完走（環境起因）/ revisions.json の内容diff未検証 / sleep 1回

## 総合判定
**✅ 合格**（起動✅ / 完走・自走✅ / goal 合格）

## 推奨アクション
- troubleshooting.md §4 に「boot 自身のドメインリロードで UnityMcpSettings.json が .bak 化される race（run-tests 非並走でも発生）」を追記する
