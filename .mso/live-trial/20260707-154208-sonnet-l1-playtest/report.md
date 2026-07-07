# live-trial report: Trial 2 — Sonnet L1（手順追従）

## 対象
- **skill**: unity-playmode-recorded-playtest
- **args**（task.md 引用）: 「tools/playtest/scenarios/belt-line-via-ui.cs を実行して結果を報告して」（Trial 1 と同一課題）
- **評価コンテキスト**: モデル別精度評価 Level 1

## model 検証（機械 gate）
- requested_model: `claude-sonnet-5`
- actual_model: `claude-sonnet-5`（transcript jq、単一値）→ **一致 ✅**

## timeline
- boot: READY 2s（SESSION_ID: 96bfdf7d-b6d5-4d75-a696-a315c7c97e44）
- poll: DONE 630s（exit 0、via jsonl、chunk 1 で終端）
- wall-clock: 約11分

## 介入
- nudge_count: **0** / gate 応答: **0**（完全自走）

## 成果物
- 完了マーカー: `out/status.json` — `{"status": "PASS", ..., "asserts_passed": 8, "asserts_total": 8, "attempts": 3}`
- result.json: `moorestech_client/PlaytestResults/20260707_155056/belt-line-via-ui/result.json`（Success:true, 8/8）
- 録画: recording.mp4（h264 1280x720 / 16.07s / 1.83MB）+ スクショ4枚
- transcript: `transcript.jsonl` / pane: `pane.txt`
- git 副作用: トライアル自身は revert 実施済みでクリーン終了（模範的）

## goal 判定（fresh evaluator）
- **goal適合: 90 / 合格**
- 成功条件4つすべて一次情報で達成（スクショ目視所見: アバター・地面・HUD・搬送中インゴットまで確認）
- 手順遵守: revert 含め基本踏襲。**主要逸脱1件** = 2回目試行で「PlayMode必須停止」をスキップ→汚染PlayMode使い回しで180sタイムアウトを自招（回避可能な失敗1サイクル）。3回目にフルストップ→フレッシュブートで自己修正
- 誤ルート: 実質0（simulate系0・固定sleep実質0・共有masterHEAD 0・ポート実害0）
- attempts 実測: 3（自称と一致。1回目=UnityMcpSettings.json退避の環境要因、2回目=自己ミス、3回目=成功）
- 切り分け精度: 正確（環境要因/自己ミスを正しく分類、プロダクトバグ誤帰責ゼロ）
- 未達点: 必須stop スキップ1件 / スクショ目視2/4枚 / 診断表の [5/5] 確認省略 / FMOD ErrorLog 未言及

## 総合判定
**✅ 合格**（起動✅ / 完走・自走✅ / goal 合格）

## Trial 1（Opus L1）との比較メモ
- 両者 PASS・介入0・誤ルート実質0・goal適合90で同水準
- Opus: attempts=2（環境失敗後、手動再構成の即興で復帰＝速いが one-shot 経路から離脱）
- Sonnet: attempts=3（手順逸脱による自招失敗1回を挟むが、最終的に one-shot 経路（フレッシュブート）へ回帰＝スキル手順への忠実度は回復力込みで同等）
- 所要時間: Opus 345s / Sonnet 630s

## 推奨アクション
- run-scenario.md 手順2「PlayMode停止（必須）」を再実行時にも適用することを強調する記述（「失敗後の再実行時も必ず stop から」）を追記
