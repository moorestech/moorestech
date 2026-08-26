# ADR 0035: スキット中のポーズメニュー表示（入れ子サブステート）

## Status
Accepted (2026-08-26)

## Context
`SkitState.GetNextUpdate()` はスキット終了しか監視せず、`InputManager.UI.OpenMenu`(Esc) を無視するためスキット中にポーズメニュー（セーブ・終了等）を開けない。
列車HUDは `TrainHudScreenUIStateController` の入れ子サブステートで `PauseMenuStateService` を再利用してこれを解決している（前例）。
スキット中のEscは `SkitUITools` で「会話UI非表示→復帰」にも使われている。webモードではスキット中のWeb UI提示が `SkitPresentationStateStore` でブロックされる。

## Decision
- `SkitState` に列車HUD同型の入れ子サブステート（Skit / PauseMenu）を持たせ、`PauseMenuStateService` を再利用してメニューを表示する。
  出所: ユーザー裁定 2026-08-26 原文「スキット画面でもメニュー画面出すようにしたい」／agent前提（TrainHudPauseMenuSubState 前例）
- メニュー表示中もスキットは背後で再生を続ける。出所: ユーザー裁定 2026-08-26「止めずに背後で流し続ける」
- Escは会話UI非表示中はUI復帰を優先し、表示中のみメニューを開く。メニュー中のEscはメニューを閉じてスキットへ戻る。出所: ユーザー裁定 2026-08-26「UI非表示中のEscはまず復帰、表示中のEscでメニュー」
- 対象はuGUIモードのみ（webモードのブロック設計は変更しない）。出所: ユーザー裁定 2026-08-26「uGUIモードのみ」
- メニュー表示中にスキットが終了したらサブステートを閉じてGameScreenへ遷移する。出所: ユーザー裁定 2026-08-26「メニューを閉じてGameScreenへ戻す」

## Considered Options（実提示・棄却）
- メニュー中にスキットを一時停止 — 実行器への停止機構新設が必要。棄却（ユーザー裁定）
- Escを常にメニューにしUI復帰を別操作へ — 既存挙動変更。棄却（ユーザー裁定）
- Web両対応 — ブロック設計への例外が必要。棄却（ユーザー裁定、後回し）
- 終了時にPauseMenuStateへ引き継ぎ — 棄却（ユーザー裁定）

## Consequences
- `SkitUITools` の「UIが非表示か」を `SkitState` から参照できる窓口が必要（Escの優先順位判定）。`Input.GetKeyDown(KeyCode.Escape)` 直読みは `InputManager.UI.CloseUI` へ寄せる候補。
- ポーズメニュー表示中もスキットUI(UIToolkit)がクリックを受ける可能性があるため、メニューのレイキャスト遮蔽を確認する。
- 関連: [[2026-08-26-スキット中のポーズメニューは入れ子ステートで背後再生継続]]
