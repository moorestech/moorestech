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

## 追記 2026-08-26: uGUIモードは存在しない — Web側へも出す

`WebUiScreenGate.IsWebUiMode` はuGUI廃止Phase1で恒久 `true`（docs/webui/ugui-retirement-plan.md）。「uGUIモードのみ」の裁定は前提が誤っていたため、列車HUD(`trainPause`)同型でWeb UIに表示する。
C#: `SkitPlayingSubState` のwebガード撤去／`SkitState.SubState`・`OnPresentationChanged` 公開／`UiStateTopic` がStory中もsubStateを配信／`UiStateActions` はStory中のGameScreen要求で入れ子のみ閉じる。Web: `uiScreenRouting` に `skitPause`、`App.tsx` で `PauseMenuPanel`。
出所: ユーザー裁定 2026-08-26「Web側にも出す（列車HUD同型）」（.decisions/2026-08-26-スキット中ポーズメニューはWeb側にも出す（uGUIモードは存在しない）.md）

## 追記 2026-08-27: 入れ子ポーズを型で束ね、Web契約は寛容に

レビュー裁定4件（.decisions/2026-08-27-入れ子ポーズ画面はINestedPauseScreenStateで束ねWeb契約は寛容にする.md）を実装した。

- 「入れ子ポーズを持つ画面」を `INestedPauseScreenState`（`SubStateName` / `OnPresentationChanged` / `bool RequestClosePauseMenu()`）で束ね、`TrainHUDScreenState` と `SkitState` が実装する。`UiStateTopic` の subState 解決と購読、`UiStateActions` の閉じ分岐はいずれも `is INestedPauseScreenState` の1本に畳んだ
- サブステート実体は `State/NestedPause/`（`NestedPauseSubStateEnum` / `INestedPauseSubState` / `NestedPauseSubStateController` / `PauseMenuNestedSubState`）へ共通化し、語彙は `GameScreen`/`PauseMenuScreen` に統一。`SkitState.GetKeyHints()` はサブステートへ委譲
- 閉じ要求は実際に閉じたときだけ `true`。閉じるものが無い要求は `transition_not_allowed` で拒否する
- Web: `UiStateDataSchema.subState` は `z.string().optional()`（未知値の解釈は `screenForUiState` のfail-safe）。skitPause 中は `SkitPresentation` を `pointer-events: none` にして表示と自動送りだけ継続させる
- 到達不能だったuGUI会話UI復帰枝（`SkitUITools.IsUIHidden`/`ShowUI`・`SkitUI.ShowHiddenUI`）は削除し、`SkitManager.TryRestoreHiddenSkitUi` は store 一本で判定する
