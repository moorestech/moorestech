# live-trial report: brainstorming（スポイト設計）ベースライン再現

## 対象 skill + args（task.md 引用）
`Skill({skill: "brainstorming", args: "建設メニュー中のスポイト機能を実装したい。ミドルクリック"})`
既知のユーザー回答（有効範囲=PlaceBlock+GameScreen両方 / 向きコピー=する / 承認=全て承認）を task.md で付与し、spec+plan 作成まで自走。

## 目的
2026-07-08 の実セッションでユーザー修正指示「データフローを一貫したいので、適切なUIステート（GameScreenなど）側から毎フレーム『ミドルクリック検知 + スポイト対象検知』のサービスを動かして、そこからPlacementSelectionをセットする形にしたい」を受けた設計欠陥が、fresh context でも再現するかの確認（ベースライン）。

## model
- requested_model: claude-fable-5
- actual_model: claude-fable-5（transcript jq、単一値・一致）

## timeline
- boot: READY 2s（trust問題なし、隔離worktree: scratchpad/lt-wt-eyedropper @ df6e2c8ac detached）
- poll: 前景600sチャンク×2、exit 0 DONE (855s) via jsonl
- nudge_count: 0 / gate応答回数: 0（完全自走）

## 成果物
- spec: worktree `docs/superpowers/specs/2026-07-08-eyedropper-design.md`（commit ef1917108 → 本ディレクトリ spec-snapshot.md に保全）
- plan: worktree `docs/superpowers/plans/2026-07-08-eyedropper.md`（commit 6bfcb7e93 → plan-snapshot.md に保全）
- 完了マーカー: `out/status.json` = `{"status": "PASS", "spec_path": "...2026-07-08-eyedropper-design.md", "plan_path": "...2026-07-08-eyedropper.md"}`
- transcript: `transcript.jsonl`（本ディレクトリ）/ pane.txt 保全済み
- 副作用: 隔離worktree内の2コミットのみ（メインリポジトリへの影響なし。worktreeは検証後リセット）

## goal 判定（fresh evaluator: fable / low effort）
- goal適合スコア: **20/100**
- 合否: **FAIL**（修正指示は確実に必要だった＝欠陥再現）
- 未達点:
  - A（毎フレームManualUpdate駆動）: 不適合 — `public bool TryPick()` をクリック検知後にのみ呼び、bool戻り値で遷移制御
  - B（クリック検知もサービス内部）: 不適合 — 中クリック判定はステート側（`中クリック → TryPick()`）
  - C（PlacementSelection一本）: 不適合 — 向きを `CommonBlockPlaceSystem.SetPlaceDirection` / `BeltConveyorPlaceSystem.SetPlaceDirection` 直呼びで配布（別データ経路を新設）
- 備考（evaluator）: 直セット案は「stale回避の上位互換」と積極的に正当化されており、偶然でなく設計判断として C に反していた

## 総合判定（判定表）
起動 ✅ / 完走（自走含む）✅ / goal 適合 ✗ → **⚠️ goal-proxy 乖離**（brainstorming は完走するが、この種の入力サービス設計でユーザーの求めるデータフロー形状を出せない）。
本 trial の文脈では「欠陥のfresh再現に成功」であり、ベースラインとしての目的は達成。

## 根本原因（transcript 分析）
1. **検査2（前例）の同族誤り**: `GameScreenSubInventoryInteractService`（TryGet型・共有状態を書かない遷移判定サービス）を前例に採用。共有選択モデル（PlacementSelection）を書き換えるサービスの同族は `PlaceSystemStateController` / `BuildViewModeController`（毎フレームManualUpdate駆動）だが、references に判別規則が無かった。
2. **検査4の藁人形棄却**: 受動案（PlacementSelection拡張）を「同一ブロック再ピックで IsSelectionChanged が発火しない」で棄却。実際は比較フィールド追加（または SelectionVersion）で解決可能であり、直接セッター新設の理由にならない——この反駁規則も references に無かった。

## 推奨アクション（実施済み）
- `writing-plans/references/moorestech-layer-map.md`: 機構規約表に「共有選択モデルを書き換える入力サービス」行を追加、よく引っかかる箇所に2項目追加
- `brainstorming/references/moorestech-principles.md`: 「UI入力・選択モデル（クライアント）」セクションを新設
- 効果検証: 後続 trial `20260708-053209-brainstorming-eyedropper-fixed`（95/100 PASS）

## 結果ビューアー
- URL: http://localhost:4981 （curl 200 確認済み）
- 対象ディレクトリ: /Users/katsumi/moorestech/.mso/live-trial
- サーバー: scratchpad/lt-result-viewer/ で nohup 起動（ログ: 同ディレクトリ viewer.log）
- 注: スキル既定は sonnet subagent だが、ユーザー指示「subagent は fable 5 low」に従い fable(low) で作成

## 追記: 二重評価による確認
初回起動した評価エージェント（応答遅延のため respawn していた eval-baseline, fable/low）からも遅れて結果が到着: **15/100 FAIL（3要件とも不適合、引用箇所も eval-base2 と同一）**。独立2評価（15/100・20/100）が同一判定で一致し、ベースラインの欠陥再現は二重に確認された。
