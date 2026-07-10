# live-trial report: brainstorming（スポイト設計）SKILL.md「データフロー地図」検査＋Opus汎化検証

## 位置づけ
先行2trial（fable: ベースライン20/100 FAIL → references修正後95/100 PASS）に続く3本目。目的は2つ:
1. SKILL.md 本文に追加した「データフロー地図」検査（references細則ではなく、配置単位をクラスでなくデータフローで捉える上位の観点）が効くか
2. fable でチューニングした修正が **別モデル Opus 4.8** でも成立するか（過学習でないことの確認）

## 対象 skill + args（task.md 引用）
`Skill({skill: "brainstorming", args: "建設メニュー中のスポイト機能を実装したい。ミドルクリック"})`
既知のユーザー回答（両ステート有効・向きコピー・全承認）付与、spec+plan 作成まで自走。task.md は先行 trial と同一。

## 検証対象の skill 状態（HEAD 6eb525bfb）
- `e06a39072` references 追記（先行 trial で検証済み）
- `6eb525bfb` SKILL.md「データフロー地図」検査を新規追加（本 trial の主対象）:
  - writing-plans: Phase 1.5「データフロー地図」— 矢印列を書き新規要素を書き手/読み手/交差点で宣言、bool戻り・迂回セッター・イベント型混入を交差点として弾く
  - brainstorming: Phase 1 観点表に「データフロー参加位置」行＋最初に確定させる段落

## model
- requested_model: claude-opus-4-8
- actual_model: **claude-opus-4-8**（transcript jq、単一値・一致）※fable系trialと異なるモデルでの実走を実証

## timeline
- boot: READY 2s（隔離worktree lt-wt-eyedropper-opus @ 6eb525bfb detached、両修正入りを grep 確認済み）
- poll: 前景600sチャンク×2、exit 0 DONE (810s) via jsonl
- nudge_count: 0 / gate応答回数: 0（完全自走）

## 成果物
- spec: worktree `docs/superpowers/specs/2026-07-08-block-eyedropper-design.md`（commit 7f50c342e → spec-snapshot.md 保全）
- plan: worktree `docs/superpowers/plans/2026-07-08-block-eyedropper.md`（commit de867b98d → plan-snapshot.md 保全）
- 完了マーカー: `out/status.json` = `{"status": "PASS", "spec_path": "...", "plan_path": "..."}`
- transcript: `transcript.jsonl` / pane.txt 保全済み

## 因果確認（新検査→行動）
- Opus transcript: 「データフロー」を **9回** 参照
- spec 冒頭に「## データフロー」節を自発生成し矢印列を記述（`UIStateControl.Update（毎フレーム）→ BlockPickService.ManualUpdate → PlacementSelection へ書き込み`）
- spec 本文「本機能の実体は『共有選択モデルへの書き手が1人増える』だけ」＝新 SKILL.md の文言をそのまま適用
- plan Architecture 行「`PlacementSelection`への2人目の書き手として `BlockPickService` を足すだけ」

## 設計形状（3要件）
- A（毎フレームManualUpdate駆動）: 適合 — `BlockPickService.ManualUpdate()`（void）を両ステートの GetNextUpdate が毎フレーム呼ぶ
- B（検知はサービス内部）: 適合 — ミドルクリック検知・レイキャスト解決・選択書き込みを全てサービス内で完結
- C（PlacementSelection一本）: 適合 — 向きは `PlacementSelection.SelectedBlockDirection` フィールドで運搬、変化検知にも含める。`SetPlaceDirection` 等の第2経路は plan に 0 件
- **GameScreen→PlaceBlock 遷移の解決**: `BlockPickService.OnPicked`（UniRx `IObservable<Unit>`）を GameScreen が購読し、自ステートの遷移だけ判断（受動的 reader）。先行 fable trial の bool 戻り値（evaluator がグレー指摘）より綺麗な解——サービスの戻り契約を呼び出し側の制御に結合させていない

## goal 判定（fresh evaluator）
- 評価者モデル: **sonnet**（当初 fable/low を指定したが Fable 5 の利用上限枯渇で eval-opus/eval-opus2 が起動不能。同一セッション外の fresh context で独立性は担保）
- goal適合スコア: **92/100**
- 合否: **PASS**（この設計なら実ユーザーの修正指示は不要だった）
- 判定内訳:
  - A（毎フレームManualUpdate駆動）: 適合 — 両ステートの GetNextUpdate 冒頭で `_blockPickService.ManualUpdate()` を毎フレーム呼ぶ（クリック時起動ではない）
  - B（検知はサービス内部）: 適合 — ミドルクリック検知・レイキャスト解決・選択書き込みをサービス内で完結、GameScreen は OnPicked 購読のみでクリック判定を持たない
  - C（PlacementSelection一本）: 適合 — 向きも同経路で伝搬、CommonBlockPlaceSystem への直接セッター新設なし（plan で SSOT として明示禁止）
- 備考（evaluator）: GameScreen→PlaceBlock 遷移は OnPicked（UniRx）購読方式。bool 直接戻り値ではなくイベント購読を選び、要件Bと整合。エッジケース（同一ID異向き再ピック・縦向き12値・空ピック・UI上クリック）の扱いも一貫し全体の質が高い

## 総合判定
起動 ✅ / 完走（nudge 0・gate 0・自走）✅ / goal 適合 **PASS 92/100** → **✅ 合格**
Opus（fableと別モデル）でも SKILL.md「データフロー地図」検査＋references が効き、正解形状を自力生成。修正が特定モデルへの過学習でないことを確認。
