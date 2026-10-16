# 削除ツール: 選択積み上げ・F長押し確定・箱選択 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 削除ツールを「なぞり・箱で対象を選択へ積み上げ、F を 2 秒長押しして一括撤去する」方式へ変え、Shift+左クリック 2 点の直方体（箱）による範囲選択を足す。

**Architecture:** クライアントのみの変更。`DeleteObjectState` が毎フレーム `DeleteObjectService.Update(deltaTime, isPointerOverUi)` を駆動し、サービスが入力（左ドラッグのなぞり／Shift+左クリックの箱／F 長押し／Q・E）を検知して選択モデル `DeleteSelection` へ「足す・ホバー赤・全解除・確定」を指示する。赤プレビューの書き手は `DeleteSelection` の 1 本。箱は `DeleteBoxInput`（入力・セル解決・可視化）→ `DeleteBoxTargetResolver`（AABB に入る撤去可能な `IDeleteTarget` の列を返す 1 本の問い合わせ。撤去不可はここでログして除く）→ `DeleteSelection.AddBoxTargets` を `DeleteBoxInput` 内部で直接呼ぶ一方向。長押しは `DeleteHoldConfirm` が判別子付きの `DeleteHoldSubject` を受け、採掘と同じ `ProgressBarState` へ進捗を流す。サーバー・プロトコルは変更しない。

**Tech Stack:** Unity (C#)、UniRx、Unity InputSystem（`InputManager` / `HybridInput`）、NUnit EditMode テスト（`Client.Tests`）、unity-playmode-recorded-playtest（プレイテスト DSL）。

## 現行Interfaces（レビュー反映後）

- `IDeleteTarget.OnDeleteCompleted: IObservable<DeleteCompletion>`。Pending/Succeeded/Rejected/Unconfirmedと論理対象キー・理由を運搬する。既存個別応答を使い、車両だけ既存snapshotのtick完了後の不在・直列要求への拒否通知を使う。
- `DeleteSelection.UpdateStroke(DeleteAimResult)`、`UpdateHoverPreview(IDeleteTarget,out LocalizationKey?)`、`TryCommit(DeleteHoldSubject,out LocalizationKey?)`、`TryCancelSelection()`。`OnSelectionChanged`がRevision変更を通知する。
- `DeleteHoldConfirm.SetSubject(DeleteHoldSubject)`、`Advance(bool isKeyHeld,float deltaTime)`。選択変化は通知で対象をプッシュし、入力と照準は従来のフレーム更新で取得する。
- `DeleteCommitBatch`が対象別復元情報を採取し成功分だけで履歴予約を完成する。車両未確認はUndo完成条件に含めない。`DeleteResultFeedback`がホバーと独立した結果と通知を保持する。
- `BezierCellBoxIntersection.Intersects(Vector3Int min,Vector3Int max)`。ownerのSetControlPointsで係数を作り、六境界との交差を判定する。旧samplerは廃止。
- `RightShortPressInputService.TryConsumeShortPressOutsideUi(bool)`、`PlacementTargetPickService.TryPickTargetUnderCursor(bool,out IPlacementTarget)`、`DeleteObjectService.Update(float,bool)`はstateで一度得たUI判定を共有する。
- `DeleteBoxSelection.EndCell`は最後の有効終点。無効カーソル中もその可視箱を再解決して件数を更新し、理由表示と確定拒否を行う。

以下の初期実装手順内のコード例は作業記録であり、レビュー反映で上記と末尾の委任既定が置き換える。Requirements/Global Constraintsは引き続き制約とする。

## Requirements

設計 ADR: `docs/adr/0079-delete-tool-accumulates-selection-and-confirms-with-f-hold-and-box-select.md`。裁定: `.decisions/2026-10-16-削除ツール*.md`（12 件）。

- R1 なぞり選択（左ドラッグ）は残す。左ボタンを離しても選択は消えず、何度なぞっても同じ選択集合へ追加される。受け入れ: なぞって離した後も対象は赤プレビューのまま残り、撤去要求は送られない。
- R2 1 回のなぞり（ストローク）に入るのは同じ破壊カテゴリーだけ（現行どおり）。カテゴリー固定はストローク開始でリセットされ、左ボタンを離した時点でも最前面へ戻り、箱の影響を受けない。受け入れ: 既存 `DeleteSelectionCategoryTest` 相当が通り、離した直後のホバーは別カテゴリーも拾う。
- R3 削除の確定は F キー長押し（`InputManager.Playable.Interact`）。件数に関係なく一定 2 秒（定数）。進捗は採掘と同じ画面固定の進捗バー（`ProgressBarState`）へ 0→1 で出す。受け入れ: 2 秒未満で離すと進捗が 0 に戻り選択は残る。満了で選択を全部撤去し 1 つの Undo バッチを積む。
- R4 F 長押しは「F を離す」「選択集合が変わる」「（空選択時）ホバー対象が変わる」「ESC（箱を畳む段・選択を解除する段のどちらでも）」のいずれでも進捗 0 に戻る。満了後と押下中のキャンセル後は F を一度離すまで次の長押しを始めない。受け入れ: `DeleteHoldConfirmTest`。
- R5 選択が空で F を長押しすると、ホバー中の 1 件（撤去可能なもの）を暗黙の選択として撤去する。選択が 1 件以上あればホバー対象は含めない。ホバーも選択も無ければ進捗バーを出さない。
- R6 箱選択: Shift を押しながら左クリック（UI 外で押下→解放）で始点セル、次の左クリックで終点セル。セルは 1x1 設置と同じ `PlacementUnitCellResolver.TryGetCursorCell`。Q/E は設置モードと共用の `PlacementHeightOffset`（DI シングルトン。HUD に高さが出る）を動かし、始点・終点どちらにも効く。箱を畳む・削除モードを離れるときに `ResetToGround()` で地表へ戻し、設置モードへ持ち越さない。
- R7 箱は始点・終点セルを対角とする AABB（セル [min, max] を含む直方体）。破壊カテゴリーを問わず、占有セルが 1 つでも交差するブロック・曲線上の点が 1 つでも箱内のレール edge・位置が箱内の列車車両を全て選択へ足す。接続線は箱では選ばない。遮蔽は問わない（レイキャスト不使用）。
- R8 箱の終点待ち中はカーソルツールチップへ「範囲内の対象: N」（撤去可能なものの数）を常時出し、0 件の終点クリックは追加せずツールチップ（「範囲に対象がありません」）とログへ理由を残して終点待ちのまま。終点待ち中は左クリックを終点確定に使い、なぞりは始まらない。
- R9 箱で足した対象は選択時に固定（対象の集合）。後から箱内へ入った対象は含めず、選んだ車両が動いても消す。選択後に破棄された対象は確定時に読み飛ばす（既存の `destroyed before commit`）。
- R10 選択の個別除外は作らない。ESC／パネル外右短押しは「終点待ちの箱を畳む → 選択の全解除 → 破壊モード終了」の順で 1 段ずつ効く。モード離脱（G/B/Tab/スポイト）で選択・箱・進捗・高さを全て片付ける。
- R11 選択がある間はカーソルツールチップに「選択中: N件」を常時出す。拒否理由（異なるカテゴリー・撤去不可）は同じツールチップの別行として出す。
- R12 箱で非撤去可の対象（駅内部レール等）は `DeleteBoxTargetResolver` が件数と理由を `Debug.Log` へ残して除く（無音の縮退禁止）。同一箱操作の同じ理由は初回のみ記録し、箱を畳むと記録済み状態を破棄する。表示件数・0 件拒否・追加は同じ列を使う。
- R13 一部が撤去できないときは消せる対象だけ消し、拒否理由を対象ごとに現行どおり表示（サーバー変更なし・個別送信のまま）。
- R14 ホバーの赤プレビューと選択済みの赤プレビューは同じ表示。ホバーが選択済み対象から外れてもその対象の赤は戻さない。赤を付け外しする書き手は `DeleteSelection` だけ。
- R15 キーヒント（破壊モード）: 左ドラッグ まとめて選択／Shift+左クリック 範囲選択／Q 範囲の高さを下げる／E 範囲の高さを上げる／F（長押し） 選択を削除／G 破壊モード終了／B ビルドメニュー／Tab インベントリ／中クリック 設置物をスポイト／Ctrl+Z 元に戻す／V 視点切替（11 件）。ADR 0032 の表と `UIStateKeyHintCatalogTest` を同時更新。
- R16 BP コピーの範囲ビジュアライザとセル AABB 計算を共通置き場へ移し、削除ツールと共用する（BP コピーの GameObject 名 `BlueprintCopyStartMarker` 等と挙動は不変）。ブロックの箱判定は既存 `BlueprintCopyTargetRule.CreateBox` と `BlockPositionInfo.IsOverlap` をそのまま使い、複製しない。
- R17 既存の録画シナリオ（削除をクリック／ドラッグで行う 3 本）を新方式（選択後 F 長押し）へ更新し、新規の録画シナリオで「なぞり→離しても残る→F 長押しで消える／箱選択→件数→F 長押し／ESC 全解除／Ctrl+Z」を通す。レールを跨ぐ箱は橋脚撤去シナリオに 1 ステップ足して通す。
- やらないこと: 一括撤去プロトコルの新設、選択の個別除外、選択上限、サーバー側の変更、箱による接続線の直接選択、列車車両の Undo。

## Global Constraints

- AGENTS.md: 1 ファイル 200 行以下、1 ディレクトリ 10 ファイルまで（新規作成時）、partial 禁止、`Func<>` 禁止、try-catch 原則禁止、デフォルト引数禁止、イベントは UniRx、単純 getter/setter 禁止（`{ get; private set; }` は可）、`[SerializeField]` は小文字キャメル、日英 2 行コメント、`#region Internal` はメソッド内ローカル関数のみ、無音の縮退禁止（スキップ・拒否は理由をログへ。意図して黙って続行する行は `silent-ok: <理由> / <気づく経路>`）、`.meta` は手で作らない（`git mv` でファイルと `.meta` を一緒に移す）。
- 時間: クライアントの長押し進捗は採掘と同じく `Time.deltaTime` 累積（`DeleteObjectState` が `Time.deltaTime` を渡す）。サーバーの時間 API は触らない。
- 入力: F は `InputManager.Playable.Interact.GetKey`、Shift は `HybridInput.GetKey(KeyCode.LeftShift)`（前例 `CommonBlockPlaceSystem.cs:128`）、Q/E は `PlacementHeightKeyInput.Apply`、左クリックは `InputManager.Playable.ScreenLeftClick`、UI 上判定は `UiPointerHitTest.IsPointerOverAnyUi()`（1 フレーム 1 回評価）。
- ローカライズ: `Localization/localization.csv` の列は `key,Source,English,日本語,Deutsch,한국어` の 6 列（言語 4）。C# 側は `uloop compile --project-path ./moorestech_client --force-recompile` の後に通常 `uloop compile`（`docs/development/uloop-troubleshooting.md`）。Web UI 側は `cd moorestech_web/webui && npm run gen:i18n` で `src/shared/i18n/generated/localizationKeys.ts` を再生成してコミット。
- 定数: 長押し満了 `DeleteHoldConfirm.HoldSeconds = 2f`。レールは連続Bezierと半開箱の交差判定（サンプル数定数なし）。
- 命名: 選択モデルは `DeleteSelection`（旧 `DragDeleteSelection`）。ディレクトリ `UI/UIState/State/DragDelete/` と名前空間 `Client.Game.InGame.UI.UIState.State.DragDelete` は据え置く。新規ファイルは `DragDelete/Box/`・`DragDelete/Confirm/` へ置く（10 ファイル制限）。
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "<FullName>"`。ドメインリロードエラー時は 45 秒待って再試行。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正1件 / 配置表・データフロー地図・死活表を plan 本文に記載。Self-Review でホバー解放時の赤プレビュー残留を見つけ修正（後に赤の書き手を DeleteSelection へ一本化）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱5・第3バケツ2 / 強: 長押し対象を判別子付き型へ・箱の件数と撤去可否の導出を Resolver に一本化（ユーザー承認）、箱局面 enum は前例どおり据え置き（ユーザー裁定）、DeleteBoxStep は構造体を消して箱入力が選択へ直接足す（ユーザー委任→推奨）。弱 5 件は全部拾う（ユーザー裁定）。第3バケツ 2 件はレビュー依頼文の「本PR外のリファクタ提案」へ

---

## File Structure

| 区分 | パス | 責務 |
|---|---|---|
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox/CellRangeBox.cs` | 2 セルから AABB（min/max）と世界座標 Bounds を計算する純関数 |
| Move | `.../PlaceSystem/Blueprint/Copy/BlueprintCopyRangeVisualizer.cs` → `.../PlaceSystem/Common/RangeBox/CellRangeBoxVisualizer.cs` | 始点・終点マーカーと範囲箱の表示（名前接頭辞と範囲色を外から渡す） |
| Modify | `.../PlaceSystem/Blueprint/Copy/BlueprintCopySystem.cs` | 共通ビジュアライザへ差し替え |
| Modify | `.../PlaceSystem/Blueprint/Copy/BlueprintCopySelection.cs` | `CalcBox` を `CellRangeBox.Calc` へ委譲 |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxRule.cs` | 点・曲線サンプルが箱に入る判定の純関数（ブロックは既存 `BlueprintCopyTargetRule.CreateBox`＋`IsOverlap` を使う） |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxRailSampler.cs` | `BezierRailChain` の曲線を 64 点サンプルして箱と照合 |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs` | AABB に入る撤去可能な `IDeleteTarget` の列を返す 1 本の問い合わせ（ブロック・レール・車両。撤去不可はログして除く） |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs` | 箱の局面（Idle/SelectingEnd）と始点セルのモデル |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs` | Shift+クリック／クリック／Q・E の検知、セル解決、可視化、終点確定時に `DeleteSelection.AddBoxTargets` を直接呼ぶ。左押下の所有判定 `ClaimsLeftPress()` |
| Create | `.../UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs` | 長押し対象の判別子付き型（None／Selection(revision)／Hovered(key)） |
| Create | `.../UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs` | F 長押しの進捗（2 秒）と進捗バー駆動 |
| Rename | `.../UI/UIState/State/DragDelete/DragDeleteSelection.cs` → `DeleteSelection.cs` | 選択集合（持続・箱追加・ホバー赤・Revision・`DeleteAddOutcome`） |
| Create | `.../UI/UIState/State/DragDelete/DeleteAddOutcome.cs` | `TryAddTarget` の結果（Added／AlreadySelected／StrokeDead／NotRemovable(reason)／CategoryMismatch） |
| Modify | `.../UI/UIState/State/DragDelete/DeleteObjectService.cs` | 毎フレームの司令塔（箱→なぞり→ホバー→長押し→ツールチップ） |
| Modify | `.../UI/UIState/State/DeleteObjectState.cs` | 依存追加（ProgressBarState・Camera・PlacementHeightOffset・DataStore）、deltaTime 受け渡し、ヒント 11 件 |
| Modify | `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs` | `EnumerateRailChains()` 追加 |
| Modify | `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarObjectDatastore.cs` | `EnumerateTrainCars()` 追加 |
| Modify | `Localization/localization.csv`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | キー追加・再生成 |
| Modify | `docs/adr/0032-key-hint-hud-owned-by-ui-state.md` | 破壊モード行を 11 件へ |
| Test | `Client.Tests/PlaceSystem/Common/RangeBox/CellRangeBoxTest.cs`、`Client.Tests/UIState/DragDelete/DeleteBoxRuleTest.cs`、`Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs`、`Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs`、`Client.Tests/UIState/DeleteSelectionTest.cs`（旧 `DragDeleteSelectionTest.cs`）、既存 `DragDeleteDenyReasonTest.cs`／`DeleteSelectionCategoryTest.cs`／`DragDelete/DestroyedSelectionTest.cs`／`BuildUndo/RemoveOperationHistoryTest.cs`／`UIStateKeyHintCatalogTest.cs`／`UIStateFocusRestorationTest.cs`／`UIStateCameraInteractionTest.cs`／`RightShortPressTransitionTest.cs` | 更新 |
| Scenario | `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool-box-select-hold-confirm.cs`（新規）、`scenarios/building/build-undo-ctrl-z.cs`／`scenarios/train/delete-tool-pier-removal-undo-rail.cs`／`scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs`（更新） | unityプレイ録画テスト |

### データフロー地図（Phase 1.5）

```
（DeleteObjectState.GetNextUpdate 毎フレーム）
  → DeleteObjectService.Update(deltaTime)
      → DeleteBoxInput.Update(selection, lines, isPointerOverUi)   … Shift+クリック/クリック/Q・E → DeleteBoxSelection（局面）→ 終点確定時 Resolver.Resolve(min,max) → selection.AddBoxTargets（箱入力が直接書く）
      → DeleteSelection.BeginStroke / TryAddTarget / EndStroke / SetHoverPreview / CancelSelection / CommitDelete   ［共有選択集合＝唯一の書き込み先・赤プレビューの唯一の書き手］
      → DeleteHoldConfirm.Advance(F, DeleteHoldSubject, dt) → 満了で DeleteSelection.CommitDelete
      → IMouseCursorTooltip.Show(lines)           … 読み手（件数・拒否理由）
      → ProgressBarState.SetProgress              … 読み手（進捗 HUD）
      → PlacementHeightOffset（DI 共用）           … Q/E の書き手（BP コピーと同じ）。HUD の PlacementModeTopic が読む
```

新規コンポーネントは全て「DeleteSelection への書き手」か「読み手」であり、既存フローへの bool 戻り値の逆流や第 2 の書き込み経路は足さない。既存の `DeleteTargetRaycaster.AimAt(filter)`（照準）と `RemoveOperationRecord`（Undo 記録）はそのまま使う。

### 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| 入力→選択反映サービスをステートから毎フレーム駆動 | `DeleteObjectService.Update(deltaTime, isPointerOverUi)` | `PlaceSystemStateController.ManualUpdate()`、既存 `DeleteObjectService.Update()` |
| 長押し進捗 | `DeleteHoldConfirm` → `ProgressBarState`（同アセンブリの internal Show/Hide/SetProgress） | `MiningProgressState`（`Client.Game/InGame/Mining/`） |
| 長押し対象の判別子付き型 | `DeleteHoldSubject`（Kind＋Key） | `DeleteAimFilter`（`.decisions/2026-10-05-削除ツールの照準条件は判別unionで表す.md`） |
| 2 クリックのセル AABB・Q/E（DI の高さオフセット共用） | `DeleteBoxInput` が `PlacementUnitCellResolver`・`PlacementHeightKeyInput`・DI の `PlacementHeightOffset` を使う | `BlueprintCopySystem`（`.decisions/2026-10-08-BPコピー*.md`、`BlueprintCopySystem.cs:39`） |
| 範囲の可視化 | 共通 `CellRangeBoxVisualizer` | 旧 `BlueprintCopyRangeVisualizer` |
| ブロックの箱判定 | 既存 `BlueprintCopyTargetRule.CreateBox` ＋ `BlockPositionInfo.IsOverlap` を直接使う | `BlueprintCopyRangeCounter.cs:17,21` |
| 箱の対象解決を 1 本の問い合わせに閉じる | `DeleteBoxTargetResolver.Resolve(min,max)` が種類別判定と撤去可否を内包し、呼び出し側は列を足すだけ | 原則「判断を内包する新設サービスは指示を返す」（`.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`） |
| ツールチップ行の積み上げ | `List<TooltipLine>` を 1 フレームで組み `IMouseCursorTooltip.Show(owner, lines)` | `TapInteractionDriver.ShowHints`、`BlueprintCopyFeedbackLines` |
| 列車車両・レールの列挙 | `TrainCarObjectDatastore.EnumerateTrainCars()`、`TrainRailObjectManager.EnumerateRailChains()` | `BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary`（BP コピーの計数が使う） |

### 死活表（Phase 2.5）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 左ドラッグでなぞり選択 | 生きる（離しても残る＝裁定どおりの変更） | R1 |
| 単体のホバー→即削除 | 変わる: ホバー→F 長押し 2 秒（裁定 `.decisions/2026-10-16-削除ツールは選択が空ならF長押しでホバー中の1件を消す.md`） | R5 |
| ESC／右短押しの二段階解除（ADR 0046） | 生きる（三段階: 箱→選択→離脱） | R10 |
| G/B/Tab/中クリックの遷移 | 生きる | `DeleteObjectState.HandleTransition` 不変 |
| Ctrl+Z（撤去の取り消し） | 生きる | `BuildUndoService.ManualUpdate` 不変・`CommitDelete` が記録を積む |
| 電線ツール等の Shift+R（垂直回転） | 影響なし（破壊モードでは走らない） | 別ステート |
| 設置モードの Q/E・HUD 高さ表示 | 生きる。削除モードでも HUD に高さが出る。離脱・箱クリアで地表へ戻るので設置へ持ち越さない | R6 |
| BP コピーの始点・終点マーカー／範囲箱の名前・色 | 生きる（`"BlueprintCopy"` 接頭辞・`PlaceableColor` を渡す） | R16 |
| 録画シナリオ 3 本の削除手順 | 更新が要る（F 長押しを足す） | R17 |

---

### Task 1: セル範囲の共通部品（CellRangeBox・CellRangeBoxVisualizer）と BP コピーの差し替え

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox/CellRangeBox.cs`
- Move (`git mv` でファイルと `.meta` を一緒に): `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyRangeVisualizer.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox/CellRangeBoxVisualizer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopySystem.cs:38,62`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopySelection.cs:52-55`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/RangeBox/CellRangeBoxTest.cs`（`Common/` 直下は既に 11 ファイルなので新ディレクトリへ）

**Interfaces:**
- Consumes: `MaterialConst.GetPreviewPlaceBlockMaterial()`, `MaterialConst.PreviewColorPropertyName`, `MaterialConst.PlaceableColor`, `MaterialConst.NotPlaceableColor`（`Client.Common`）
- Produces:
  - `public static class CellRangeBox { public static (Vector3Int min, Vector3Int max) Calc(Vector3Int a, Vector3Int b); public static Bounds ToWorldBounds(Vector3Int min, Vector3Int max); }`
  - `public class CellRangeBoxVisualizer { public CellRangeBoxVisualizer(string objectNamePrefix, Color rangeColor); public void ShowSelectingStart(Vector3Int hoverCell); public void ShowSelectingEnd(Vector3Int startCell, Vector3Int hoverCell, Vector3Int min, Vector3Int max); public void HideAll(); }`（GameObject 名は `{prefix}StartMarker` / `{prefix}EndMarker` / `{prefix}RangeBox`）

- [ ] **Step 1: テストを書く**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common.RangeBox
{
    public class CellRangeBoxTest
    {
        [Test]
        public void Calcは対角2セルから成分ごとのminとmaxを返す()
        {
            var (min, max) = CellRangeBox.Calc(new Vector3Int(5, 2, -1), new Vector3Int(1, 4, 3));
            Assert.AreEqual(new Vector3Int(1, 2, -1), min);
            Assert.AreEqual(new Vector3Int(5, 4, 3), max);
        }

        [Test]
        public void ToWorldBoundsはセル境界を含む直方体になる()
        {
            var bounds = CellRangeBox.ToWorldBounds(new Vector3Int(1, 2, 3), new Vector3Int(2, 2, 5));
            Assert.AreEqual(new Vector3(2f, 2.5f, 4.5f), bounds.center);
            Assert.AreEqual(new Vector3(2f, 1f, 3f), bounds.size);
        }
    }
}
```

- [ ] **Step 2: CellRangeBox を書く**

```csharp
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox
{
    /// <summary>
    ///     対角2セルから範囲（セル [min, max] を含む直方体）を計算する純関数
    ///     Pure helpers that turn two diagonal cells into a cell range (inclusive box)
    /// </summary>
    public static class CellRangeBox
    {
        public static (Vector3Int min, Vector3Int max) Calc(Vector3Int a, Vector3Int b)
        {
            return (Vector3Int.Min(a, b), Vector3Int.Max(a, b));
        }

        // セル境界から中心とサイズを求める（max セルの外側面まで含む）
        // Derive center and size from the cell borders, including the far face of the max cell
        public static Bounds ToWorldBounds(Vector3Int min, Vector3Int max)
        {
            var size = new Vector3(max.x - min.x + 1, max.y - min.y + 1, max.z - min.z + 1);
            return new Bounds(new Vector3(min.x, min.y, min.z) + size * 0.5f, size);
        }
    }
}
```

- [ ] **Step 3: ビジュアライザを移動して汎用化する**

```bash
cd moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem
mkdir -p Common/RangeBox
git mv Blueprint/Copy/BlueprintCopyRangeVisualizer.cs Common/RangeBox/CellRangeBoxVisualizer.cs
git mv Blueprint/Copy/BlueprintCopyRangeVisualizer.cs.meta Common/RangeBox/CellRangeBoxVisualizer.cs.meta
```

`CellRangeBoxVisualizer.cs` の全文（旧ファイルの内容を次へ置き換える）:

```csharp
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox
{
    /// <summary>
    ///     始点（赤）・終点（緑）のセルと半透明の範囲箱を表示する。名前接頭辞と範囲色は利用側が決める
    ///     Shows red start and green end cells with a translucent range box; the caller picks the name prefix and range color
    /// </summary>
    public class CellRangeBoxVisualizer
    {
        private static readonly Color EndMarkerColor = new(0.3f, 0.85f, 0.35f, 1f);
        private readonly GameObject _startMarker;
        private readonly GameObject _endMarker;
        private readonly GameObject _rangeBox;

        public CellRangeBoxVisualizer(string objectNamePrefix, Color rangeColor)
        {
            _startMarker = CreateCube(objectNamePrefix + "StartMarker", MaterialConst.NotPlaceableColor);
            _endMarker = CreateCube(objectNamePrefix + "EndMarker", EndMarkerColor);
            _rangeBox = CreateCube(objectNamePrefix + "RangeBox", rangeColor);

            #region Internal

            GameObject CreateCube(string name, Color color)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                Object.Destroy(cube.GetComponent<Collider>());
                // 設置プレビュー材質を複製して色を付ける
                // Clone the placement preview material and tint it
                var material = new Material(MaterialConst.GetPreviewPlaceBlockMaterial());
                material.SetColor(MaterialConst.PreviewColorPropertyName, color);
                cube.GetComponent<MeshRenderer>().sharedMaterial = material;
                cube.SetActive(false);
                return cube;
            }

            #endregion
        }

        public void ShowSelectingStart(Vector3Int hoverCell)
        {
            PlaceCell(_startMarker, hoverCell);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        public void ShowSelectingEnd(Vector3Int startCell, Vector3Int hoverCell, Vector3Int min, Vector3Int max)
        {
            PlaceCell(_startMarker, startCell);
            PlaceCell(_endMarker, hoverCell);
            // セル境界から中心とサイズを計算する
            // Calculate center and size from cell boundaries
            var bounds = CellRangeBox.ToWorldBounds(min, max);
            _rangeBox.transform.position = bounds.center;
            _rangeBox.transform.localScale = bounds.size;
            _rangeBox.SetActive(true);
        }

        public void HideAll()
        {
            _startMarker.SetActive(false);
            _endMarker.SetActive(false);
            _rangeBox.SetActive(false);
        }

        private static void PlaceCell(GameObject cube, Vector3Int cell)
        {
            cube.transform.position = cell + new Vector3(0.5f, 0.5f, 0.5f);
            cube.transform.localScale = Vector3.one;
            cube.SetActive(true);
        }
    }
}
```

- [ ] **Step 4: BP コピーを差し替える**

`BlueprintCopySystem.cs`: `using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;` と `using Client.Common;` を足し、フィールド型と Enable を次へ。

```csharp
        private CellRangeBoxVisualizer _visualizer;
        ...
        public override void Enable()
        {
            _visualizer ??= new CellRangeBoxVisualizer("BlueprintCopy", MaterialConst.PlaceableColor);
            _selection.Clear();
            _clickInput.Reset();
        }
```

`BlueprintCopySelection.cs`: `using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;` を足し

```csharp
        public static (Vector3Int min, Vector3Int max) CalcBox(Vector3Int a, Vector3Int b)
        {
            return CellRangeBox.Calc(a, b);
        }
```

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CellRangeBoxTest|BlueprintCopy"`
Expected: ErrorCount 0 / 全 PASS（`BlueprintCopyPasteScenarioChecks` が参照する GameObject 名は不変）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/RangeBox
git commit -m "refactor: BPコピーの範囲ビジュアライザとセルAABB計算を共通のRangeBoxへ移す"
```

---

### Task 2: 箱に入る判定（DeleteBoxRule・DeleteBoxRailSampler）と列挙口の追加

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxRule.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxRailSampler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs:47-55`（`TryGetRailChain` の直後に追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarObjectDatastore.cs:11`（`_entities` の直後に追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxRuleTest.cs`

**Interfaces:**
- Consumes: `BezierRailMeshData.EvaluatePosition(BezierRailMesh mesh, float t)`（internal static、同アセンブリ `Client.Game`）、`BezierRailChain`、`TrainCarEntityObject`
- Produces:
  - `public static class DeleteBoxRule { public static bool IsPointInBox(Vector3 point, Vector3Int min, Vector3Int max); public static bool IsAnyPointInBox(IReadOnlyList<Vector3> points, Vector3Int min, Vector3Int max); }`（ブロックの判定は既存 `BlueprintCopyTargetRule.CreateBox(min,max)`＋`positionInfo.IsOverlap(box)` を使い、ここには置かない）
  - `public static class DeleteBoxRailSampler { public const int SampleCount = 64; public static bool IntersectsCellBox(BezierRailMesh mesh, Vector3Int min, Vector3Int max); }`
  - `TrainRailObjectManager.EnumerateRailChains()` : `public IEnumerable<BezierRailChain>`
  - `TrainCarObjectDatastore.EnumerateTrainCars()` : `public IEnumerable<TrainCarEntityObject>`

- [ ] **Step 1: テストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteBoxRuleTest
    {
        [Test]
        public void 点はminの面を含みmaxセルの外側面を含まない()
        {
            var min = new Vector3Int(0, 0, 0);
            var max = new Vector3Int(1, 1, 1);
            Assert.IsTrue(DeleteBoxRule.IsPointInBox(new Vector3(0f, 0f, 0f), min, max));
            Assert.IsTrue(DeleteBoxRule.IsPointInBox(new Vector3(1.99f, 1.5f, 0.5f), min, max));
            Assert.IsFalse(DeleteBoxRule.IsPointInBox(new Vector3(2f, 0.5f, 0.5f), min, max));
            Assert.IsFalse(DeleteBoxRule.IsPointInBox(new Vector3(-0.01f, 0.5f, 0.5f), min, max));
        }

        [Test]
        public void 曲線は1点でも箱内なら入り全点が外なら入らない()
        {
            var min = new Vector3Int(0, 0, 0);
            var max = new Vector3Int(0, 0, 0);
            var crossing = new List<Vector3> { new(-5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(5f, 0.5f, 0.5f) };
            var outside = new List<Vector3> { new(-5f, 0.5f, 0.5f), new(5f, 0.5f, 0.5f) };
            Assert.IsTrue(DeleteBoxRule.IsAnyPointInBox(crossing, min, max));
            Assert.IsFalse(DeleteBoxRule.IsAnyPointInBox(outside, min, max));
        }
    }
}
```

- [ ] **Step 2: DeleteBoxRule を書く**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     箱選択の「箱に入る」判定（点・曲線サンプル）。セル [min, max] を含む直方体に対する純関数。ブロックは BlueprintCopyTargetRule の箱と IsOverlap を使う
    ///     Box-selection membership for points and curve samples over the inclusive cell box [min, max]; blocks use BlueprintCopyTargetRule's box with IsOverlap
    /// </summary>
    public static class DeleteBoxRule
    {
        // min の面は含み、max セルの外側面（max+1）は含まない
        // Inclusive at the min faces, exclusive at the far faces of the max cell (max+1)
        public static bool IsPointInBox(Vector3 point, Vector3Int min, Vector3Int max)
        {
            return point.x >= min.x && point.x < max.x + 1
                   && point.y >= min.y && point.y < max.y + 1
                   && point.z >= min.z && point.z < max.z + 1;
        }

        public static bool IsAnyPointInBox(IReadOnlyList<Vector3> points, Vector3Int min, Vector3Int max)
        {
            foreach (var point in points)
            {
                if (IsPointInBox(point, min, max)) return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 3: DeleteBoxRailSampler を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     レール edge の曲線を等間隔にサンプルし、1点でも箱内なら交差とみなす
    ///     Samples a rail edge curve at even intervals; any sample inside the box counts as intersecting
    /// </summary>
    public static class DeleteBoxRailSampler
    {
        public const int SampleCount = 64;
        private static readonly List<Vector3> SampleBuffer = new(SampleCount);

        public static bool IntersectsCellBox(BezierRailMesh mesh, Vector3Int min, Vector3Int max)
        {
            // 各メッシュ子は同じ制御点を持つ（BezierRailChainSegments が全子へ同じ Point0..3 を渡す）ので先頭の1つで曲線を評価する
            // Every mesh child carries the same control points (BezierRailChainSegments hands all children the same Point0..3), so evaluate through the first one
            SampleBuffer.Clear();
            for (var i = 0; i < SampleCount; i++)
            {
                SampleBuffer.Add(BezierRailMeshData.EvaluatePosition(mesh, i / (float)(SampleCount - 1)));
            }
            return DeleteBoxRule.IsAnyPointInBox(SampleBuffer, min, max);
        }
    }
}
```

（`BezierRailMesh`・`BezierRailMeshData` の名前空間は実ファイル `Client.Game/InGame/Train/RailGraph/BezierRailMesh.cs`・`BezierRailMesh/BezierRailMeshData.cs` に合わせて `using` を調整する。`EvaluatePosition` は internal static で同アセンブリから到達できる）

- [ ] **Step 4: 列挙口を足す**

`TrainRailObjectManager.cs` の `TryGetRailChain` 直後:

```csharp
        // 箱選択が全レール edge を走査するための列挙口
        // Enumeration entry for box selection to scan every rail edge
        public IEnumerable<BezierRailChain> EnumerateRailChains()
        {
            foreach (var gobj in _railObjs.Values)
            {
                // silent-ok: 撤去済み edge は RemoveAnimation 後に Destroy される正常経路で、辞書からの除去と非同期にずれるだけ / 箱で拾えない edge があれば録画シナリオ（橋脚撤去）の件数アサートで気づく
                if (gobj == null) continue;
                yield return gobj.GetComponent<BezierRailChain>();
            }
        }
```

`TrainCarObjectDatastore.cs` の `_entities` 直後（`using System.Collections.Generic;` は既存）:

```csharp
        // 箱選択が車両を走査するための列挙口
        // Enumeration entry for box selection to scan train cars
        public IEnumerable<TrainCarEntityObject> EnumerateTrainCars()
        {
            foreach (var car in _entities.Values) yield return car;
        }
```

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.DragDelete.DeleteBoxRuleTest"`
Expected: ErrorCount 0 / 2 PASS。`TrainRailObjectManager.cs`・`TrainCarObjectDatastore.cs` が 200 行以下であることを `wc -l` で確認（`BezierRailChain.cs` は触らない）。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core/TrainCarObjectDatastore.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxRuleTest.cs
git commit -m "feat: 削除ツールの箱に入る判定（点・レール曲線）と列挙口を足す"
```

---

### Task 3: DeleteSelection（持続する選択集合・箱追加・ホバー赤の一本化・DeleteAddOutcome・Revision）

**Files:**
- Rename: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs` → `.../DragDelete/DeleteSelection.cs`（`.meta` も `git mv`）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteAddOutcome.cs`
- Rename: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionTest.cs` → `.../UIState/DeleteSelectionTest.cs`（`.meta` も）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteDenyReasonTest.cs`、`.../UIState/DeleteSelectionCategoryTest.cs`、`.../UIState/DragDelete/DestroyedSelectionTest.cs`、`.../BuildUndo/RemoveOperationHistoryTest.cs:23,30,46,47`（型名・`BeginDrag`→`BeginStroke`・`CanCommit`→`IsStrokeAlive`・`TryAddTarget(t, out r)`→`TryAddTarget(t)` の結果型）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs:2-4`（コメント中の `DragDeleteSelection.CommitDelete` → `DeleteSelection.CommitDelete`）

**Interfaces:**
- Consumes: `IDeleteTarget`、`DeleteAimFilter`、`RemoveOperationRecord.CreateFrom(List<IDeleteTarget>, IRemovalRestoreSender)`、`BuildOperationHistory.Push`
- Produces:
  ```csharp
  public enum DeleteAddKind { Added, AlreadySelected, StrokeDead, NotRemovable, CategoryMismatch }
  public readonly struct DeleteAddOutcome
  {
      public DeleteAddKind Kind { get; }
      public LocalizationKey? DenyReason { get; }   // NotRemovable（対象由来・無ければ null）/ CategoryMismatch（DifferentCategorySelection）のときだけ
      public bool IsAdded { get; }                  // Kind == Added
      public static DeleteAddOutcome Added, AlreadySelected, StrokeDead; public static DeleteAddOutcome NotRemovable(LocalizationKey? reason); public static DeleteAddOutcome CategoryMismatch();
  }
  public class DeleteSelection
  {
      public DeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender);
      public DeleteAimFilter AimFilter { get; private set; }
      public int Revision { get; private set; }          // 追加・全解除・確定のたびに +1
      public int Count { get; }
      public IDeleteTarget HoverPreview { get; }           // 今赤くしているホバー対象（無ければ null。選択済みとは別管理）
      public void BeginStroke();                           // なぞり開始: キャンセル解除とカテゴリー固定のリセット（選択は消さない）
      public void EndStroke();                             // 左ボタン解放: カテゴリー固定を最前面へ戻す（選択は消さない）
      public DeleteAddOutcome TryAddTarget(IDeleteTarget target);          // なぞり追加（カテゴリー固定あり）
      public int AddBoxTargets(IReadOnlyList<IDeleteTarget> targets);     // 箱追加（撤去可否は Resolver 済み・カテゴリー不問）。足した件数
      public void SetHoverPreview(IDeleteTarget hovered);  // ホバー替え（旧ホバーが選択済みでなければ赤を戻す。null で解除）
      public bool Contains(IDeleteTarget target);
      public void CancelSelection();                       // 全解除（選択とホバーの赤を戻す・ストロークを死に状態へ）
      public void CommitDelete();                          // 一括撤去＋Undo 1バッチ（ホバーの赤も戻す）
      public bool IsStrokeAlive();                         // ESC後に離すまで false
      public bool HasSelection();
  }
  ```
  赤プレビュー（`SetRemovePreviewing`／`ResetMaterial`）を呼ぶのはこのクラスだけ。

- [ ] **Step 1: テストを書く（`DeleteSelectionTest.cs` 全文。旧ファイルの 6 ケースは新 API で残す）**

```csharp
using System.Collections.Generic;
using Client.Tests.BuildUndo;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.UIState.Fakes;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     DeleteSelectionの選択ロジックを検証するテスト
    ///     Tests verifying the selection logic of DeleteSelection
    /// </summary>
    public class DeleteSelectionTest
    {
        private static DeleteSelection Create() => new(new BuildOperationHistory(), new FakeRemovalRestoreSender());

        [Test]
        public void AddRemovableTargetAddsAndPreviews()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            Assert.AreEqual(DeleteAddKind.Added, selection.TryAddTarget(target).Kind);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.IsTrue(selection.Contains(target));
        }

        [Test]
        public void AddSameTargetTwiceReportsAlreadySelected()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target);
            Assert.AreEqual(DeleteAddKind.AlreadySelected, selection.TryAddTarget(target).Kind);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.AreEqual(1, selection.Count);
        }

        [Test]
        public void AddNonRemovableTargetReturnsReason()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = false, DenyReason = LocalizationKeys.Ui.Delete.StationInternalRail };
            selection.BeginStroke();
            var outcome = selection.TryAddTarget(target);
            selection.CommitDelete();
            Assert.AreEqual(DeleteAddKind.NotRemovable, outcome.Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.StationInternalRail, outcome.DenyReason);
            Assert.AreEqual(0, target.SetPreviewCount);
            Assert.AreEqual(0, target.DeleteCount);
        }

        [Test]
        public void CancelSelectionResetsAndKillsStroke()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target);
            selection.CancelSelection();
            Assert.AreEqual(1, target.ResetCount);
            Assert.IsFalse(selection.IsStrokeAlive());
            Assert.IsFalse(selection.HasSelection());
            Assert.AreEqual(DeleteAddKind.StrokeDead, selection.TryAddTarget(target).Kind);
        }

        [Test]
        public void CommitDeleteDeletesTargetThenClears()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target);
            selection.CommitDelete();
            selection.CommitDelete();
            Assert.AreEqual(1, target.DeleteCount);
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void BeginStrokeAfterCancelRevivesStroke()
        {
            var selection = Create();
            selection.BeginStroke();
            selection.TryAddTarget(new FakeDeleteTarget { Removable = true });
            selection.CancelSelection();
            selection.BeginStroke();
            Assert.IsTrue(selection.IsStrokeAlive());
        }

        // 新方式: なぞりを離しても選択は残り、カテゴリー固定は EndStroke で最前面へ戻る
        // New behavior: the selection survives strokes and the category fix returns to frontmost on EndStroke
        [Test]
        public void EndStrokeKeepsSelectionAndResetsCategory()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(line);
            var mismatch = selection.TryAddTarget(block);
            Assert.AreEqual(DeleteAddKind.CategoryMismatch, mismatch.Kind);
            Assert.AreEqual(LocalizationKeys.Ui.Delete.DifferentCategorySelection, mismatch.DenyReason);
            Assert.IsTrue(selection.AimFilter.IsCategoryRequired);
            selection.EndStroke();
            Assert.IsFalse(selection.AimFilter.IsCategoryRequired);
            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(block).IsAdded);
            Assert.AreEqual(2, selection.Count);
            Assert.IsTrue(selection.Contains(line));
        }

        [Test]
        public void AddBoxTargetsIgnoresCategoryAndDedupes()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            selection.BeginStroke();
            selection.TryAddTarget(line);
            var block = new FakeDeleteTarget { Removable = true };
            var added = selection.AddBoxTargets(new List<IDeleteTarget> { block, block, line });
            Assert.AreEqual(1, added);
            Assert.AreEqual(2, selection.Count);
            Assert.AreEqual(1, block.SetPreviewCount);
        }

        [Test]
        public void HoverPreviewSwapsRedButKeepsSelectedRed()
        {
            var selection = Create();
            var selected = new FakeDeleteTarget { Removable = true };
            var other = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(selected);
            selection.SetHoverPreview(selected);
            selection.SetHoverPreview(other);
            Assert.AreEqual(0, selected.ResetCount, "a selected target keeps its red when the hover leaves it");
            Assert.AreEqual(1, other.SetPreviewCount);
            selection.SetHoverPreview(null);
            Assert.AreEqual(1, other.ResetCount);
            Assert.IsNull(selection.HoverPreview);
        }

        [Test]
        public void CancelAndCommitAlsoResetHoverPreview()
        {
            var selection = Create();
            var hovered = new FakeDeleteTarget { Removable = true };
            selection.SetHoverPreview(hovered);
            selection.CancelSelection();
            Assert.AreEqual(1, hovered.ResetCount);
            Assert.IsNull(selection.HoverPreview);
            selection.SetHoverPreview(hovered);
            selection.CommitDelete();
            Assert.AreEqual(2, hovered.ResetCount);
        }

        [Test]
        public void RevisionAdvancesOnAddCancelAndCommit()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            var r0 = selection.Revision;
            selection.BeginStroke();
            selection.TryAddTarget(target);
            var r1 = selection.Revision;
            selection.TryAddTarget(target);
            Assert.AreEqual(r1, selection.Revision, "duplicate add must not advance the revision");
            selection.CommitDelete();
            var r2 = selection.Revision;
            selection.CancelSelection();
            Assert.Greater(r1, r0);
            Assert.Greater(r2, r1);
            Assert.Greater(selection.Revision, r2);
        }
    }
}
```

- [ ] **Step 2: DeleteAddOutcome を書く**

```csharp
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    public enum DeleteAddKind
    {
        Added,
        AlreadySelected,
        StrokeDead,
        NotRemovable,
        CategoryMismatch,
    }

    /// <summary>
    ///     なぞり追加の結果。失敗理由を種類で区別し、表示すべき拒否理由があるときだけキーを運ぶ
    ///     Outcome of a stroke add: failure kinds are distinct, and a deny-reason key rides along only when one is displayable
    /// </summary>
    public readonly struct DeleteAddOutcome
    {
        public static DeleteAddOutcome Added => new(DeleteAddKind.Added, null);
        public static DeleteAddOutcome AlreadySelected => new(DeleteAddKind.AlreadySelected, null);
        public static DeleteAddOutcome StrokeDead => new(DeleteAddKind.StrokeDead, null);
        public static DeleteAddOutcome NotRemovable(LocalizationKey? reason) => new(DeleteAddKind.NotRemovable, reason);
        public static DeleteAddOutcome CategoryMismatch() => new(DeleteAddKind.CategoryMismatch, LocalizationKeys.Ui.Delete.DifferentCategorySelection);

        public DeleteAddKind Kind { get; }
        public LocalizationKey? DenyReason { get; }
        public bool IsAdded => Kind == DeleteAddKind.Added;

        private DeleteAddOutcome(DeleteAddKind kind, LocalizationKey? denyReason)
        {
            Kind = kind;
            DenyReason = denyReason;
        }
    }
}
```

- [ ] **Step 3: DeleteSelection を書く（`git mv` 後の全文）**

```bash
cd moorestech_client/Assets/Scripts
git mv Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs
git mv Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs.meta Client.Game/InGame/UI/UIState/State/DragDelete/DeleteSelection.cs.meta
git mv Client.Tests/UIState/DragDeleteSelectionTest.cs Client.Tests/UIState/DeleteSelectionTest.cs
git mv Client.Tests/UIState/DragDeleteSelectionTest.cs.meta Client.Tests/UIState/DeleteSelectionTest.cs.meta
```

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     削除ツールの選択集合。なぞり・箱で積み上げ、ストロークを跨いで持続し、確定時のUndo記録も担う。赤プレビューの付け外しはここだけが行う
    ///     The delete tool's selection set: accumulated by strokes and boxes, persists across strokes, records undo on commit; only this class toggles red previews
    /// </summary>
    public class DeleteSelection
    {
        private readonly BuildOperationHistory _buildOperationHistory;
        private readonly IRemovalRestoreSender _restoreSender;

        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private bool _strokeCanceled;

        // ストロークごとに最初の対象のカテゴリーで照準を固定
        // Fix aim to the first target's category, per stroke
        public DeleteAimFilter AimFilter { get; private set; } = DeleteAimFilter.Frontmost;

        // 集合が変わるたびに進む。長押しの「対象が変わった」検知に使う
        // Advances whenever the set changes; the hold confirm uses it to detect a changed subject
        public int Revision { get; private set; }
        public int Count => _selectedTargets.Count;

        // 今赤くしているホバー対象（選択済みとは別管理）
        // The hovered target currently shown red (tracked apart from the selection)
        public IDeleteTarget HoverPreview { get; private set; }

        public DeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender)
        {
            _buildOperationHistory = buildOperationHistory;
            _restoreSender = restoreSender;
        }

        // ストローク開始: キャンセル解除とカテゴリー固定のリセットだけで、選択は消さない
        // Stroke start: only revives the stroke and resets the category fix; the selection survives
        public void BeginStroke()
        {
            _strokeCanceled = false;
            AimFilter = DeleteAimFilter.Frontmost;
        }

        // 左ボタン解放: カテゴリー固定を最前面へ戻す（離した直後のホバーが別カテゴリーも拾えるように）
        // Button release: return the category fix to frontmost so the next hover can pick any category
        public void EndStroke()
        {
            AimFilter = DeleteAimFilter.Frontmost;
        }

        public DeleteAddOutcome TryAddTarget(IDeleteTarget target)
        {
            if (_strokeCanceled) return DeleteAddOutcome.StrokeDead;
            if (!target.IsRemovable(out var denyReason)) return DeleteAddOutcome.NotRemovable(denyReason);

            // ストロークカテゴリーと異なるカテゴリーは追加しない（混在防止）
            // Reject a target whose category differs from the stroke category (prevents mixing)
            if (!AimFilter.Accepts(target)) return DeleteAddOutcome.CategoryMismatch();

            // 最初の追加でストロークのカテゴリーを固定する
            // Fix the stroke category on the first added target
            if (!AimFilter.IsCategoryRequired) AimFilter = DeleteAimFilter.Category(target.GetDestructionCategory());
            return Add(target) ? DeleteAddOutcome.Added : DeleteAddOutcome.AlreadySelected;
        }

        // 箱の対象（撤去可否は Resolver が判定済み）をカテゴリーを問わず足す。足した件数を返す
        // Add box targets (already filtered for removability by the resolver) regardless of category; returns how many were added
        public int AddBoxTargets(IReadOnlyList<IDeleteTarget> targets)
        {
            var added = 0;
            foreach (var target in targets)
            {
                if (Add(target)) added++;
            }
            return added;
        }

        // ホバー替え: 旧ホバーが選択済みでなければ赤を戻し、新ホバーを赤にする（null で解除）
        // Hover swap: un-red the previous hover unless it is selected, then red the new one (null clears)
        public void SetHoverPreview(IDeleteTarget hovered)
        {
            if (ReferenceEquals(HoverPreview, hovered)) return;
            ResetHoverPreview();
            HoverPreview = hovered;
            if (HoverPreview != null && !Contains(HoverPreview)) HoverPreview.SetRemovePreviewing();
        }

        public bool Contains(IDeleteTarget target)
        {
            return _selectedTargets.ContainsKey(target.GetDeleteTargetKey());
        }

        // 選択を全てリセットしてストロークを死に状態にする（ESC操作）
        // Reset all selections and mark the stroke as canceled (ESC behavior)
        public void CancelSelection()
        {
            foreach (var target in _selectedTargets.Values)
            {
                if (IsDestroyed(target, "cancel")) continue;
                target.ResetMaterial();
            }
            _selectedTargets.Clear();
            ResetHoverPreview();
            HoverPreview = null;
            _strokeCanceled = true;
            AimFilter = DeleteAimFilter.Frontmost;
            Revision++;
        }

        // 選択を一括削除し、Ctrl+Z用のUndo履歴も記録する
        // Delete the whole selection and record the undo history for Ctrl+Z
        public void CommitDelete()
        {
            // 削除送信後に端点が消え得るため撤去物を先に記録する
            // Record removed objects before sends can remove their endpoints
            var committed = new List<IDeleteTarget>();
            foreach (var target in _selectedTargets.Values)
            {
                if (IsDestroyed(target, "commit")) continue;
                committed.Add(target);
            }
            var record = RemoveOperationRecord.CreateFrom(committed, _restoreSender);
            foreach (var target in committed)
            {
                // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す
                // Delete is async over the server, so we just clear the red preview immediately
                target.Delete();
                target.ResetMaterial();
            }
            _selectedTargets.Clear();
            ResetHoverPreview();
            HoverPreview = null;
            AimFilter = DeleteAimFilter.Frontmost;
            Revision++;

            // Ctrl+Z用のUndo履歴を記録（空バッチはPushしない）
            // Record the undo history for Ctrl+Z (skip empty batches)
            if (record.HasRemovedObjects) _buildOperationHistory.Push(record);
        }

        public bool IsStrokeAlive()
        {
            return !_strokeCanceled;
        }

        public bool HasSelection()
        {
            return _selectedTargets.Count > 0;
        }

        // 既に同じ論理対象が選択済みなら何もしない（Revisionも進めない）
        // No-op when the same logical target is already selected (the revision stays)
        private bool Add(IDeleteTarget target)
        {
            var key = target.GetDeleteTargetKey();
            if (_selectedTargets.ContainsKey(key)) return false;
            _selectedTargets.Add(key, target);
            target.SetRemovePreviewing();
            Revision++;
            return true;
        }

        // ホバーの赤を戻す（選択済みなら選択側が赤を持つので触らない・破棄済みは飛ばす）
        // Un-red the hover (a selected one keeps its red via the selection; skip destroyed ones)
        private void ResetHoverPreview()
        {
            if (HoverPreview == null) return;
            if (IsDestroyed(HoverPreview, "hover")) return;
            if (!Contains(HoverPreview)) HoverPreview.ResetMaterial();
        }

        // Unity オブジェクトの破棄済み判定（墓標は警告を出して読み飛ばす）
        // Unity destroyed-object check (tombstones are skipped with a warning)
        private static bool IsDestroyed(IDeleteTarget target, string phase)
        {
            if (target is not Object unityTarget || unityTarget != null) return false;
            Debug.LogWarning($"[DragDelete] target was destroyed before {phase}");
            return true;
        }
    }
}
```

- [ ] **Step 4: 既存テスト・シナリオの参照を置換する**

```bash
cd moorestech_client/Assets/Scripts/Client.Tests
sed -i '' -e 's/DragDeleteSelection/DeleteSelection/g' -e 's/BeginDrag()/BeginStroke()/g' -e 's/CanCommit()/IsStrokeAlive()/g' UIState/DragDeleteDenyReasonTest.cs UIState/DeleteSelectionCategoryTest.cs UIState/DragDelete/DestroyedSelectionTest.cs BuildUndo/RemoveOperationHistoryTest.cs
grep -rn "TryAddTarget(.*out " UIState BuildUndo   # 残った out 版の呼び出しを DeleteAddOutcome 版へ手で直す（例: `Assert.IsFalse(selection.TryAddTarget(t, out var r))` → `var o = selection.TryAddTarget(t); Assert.IsFalse(o.IsAdded); Assert.AreEqual(expected, o.DenyReason)`）
grep -rn "DragDeleteSelection\|BeginDrag\|CanCommit" . ../Client.Game   # DeleteObjectService.cs 以外に残っていないこと
sed -i '' 's/DragDeleteSelection\.CommitDelete/DeleteSelection.CommitDelete/g' ../../../../.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs
```

`DeleteObjectService.cs` は Task 6 で全面的に書き換えるが、このタスクのコンパイルを通すため最小限の置換（`DragDeleteSelection`→`DeleteSelection`、`BeginDrag()`→`BeginStroke()`、`CanCommit()`→`IsStrokeAlive()`、`TryAddTarget(hovered, out var denyReason)`→`var outcome = _selection.TryAddTarget(hovered); if (!outcome.IsAdded) ShowDenyReason(outcome.DenyReason);`）をここで行う。この時点では「離したら確定」のままでよい。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeleteSelectionTest|DragDeleteDenyReasonTest|DeleteSelectionCategoryTest|DestroyedSelectionTest|RemoveOperationHistoryTest"`
Expected: ErrorCount 0 / 全 PASS。`DeleteSelection.cs` が 200 行以下。

- [ ] **Step 6: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Tests/UIState moorestech_client/Assets/Scripts/Client.Tests/BuildUndo .claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs
git commit -m "feat: 削除ツールの選択集合をDeleteSelectionへ改め、持続・箱追加・ホバー赤の一本化・結果型を足す"
```

---

### Task 4: DeleteHoldSubject・DeleteHoldConfirm（F 長押し 2 秒と進捗バー）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldSubject.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs`

**Interfaces:**
- Consumes: `ProgressBarState`（`Client.Game.InGame.UI.ProgressBar`。`Show()`/`Hide()`/`SetProgress(float)` は internal・同アセンブリ、`IsShown`/`CurrentProgress` は public）
- Produces:
  ```csharp
  public enum DeleteHoldSubjectKind { None, Selection, Hovered }
  public readonly struct DeleteHoldSubject : System.IEquatable<DeleteHoldSubject>
  {
      public static DeleteHoldSubject None { get; }
      public static DeleteHoldSubject Selection(int revision);
      public static DeleteHoldSubject Hovered(object targetKey);
      public DeleteHoldSubjectKind Kind { get; }
      public bool IsNone { get; }
      // Equals/GetHashCode は Kind と Key（revision は boxed int）で比較
  }
  public class DeleteHoldConfirm
  {
      public const float HoldSeconds = 2f;
      public DeleteHoldConfirm(ProgressBarState progressBar);
      public bool IsHolding { get; }
      // 毎フレーム呼ぶ。満了したフレームだけ true を返し内部を戻す
      public bool Advance(bool isKeyHeld, DeleteHoldSubject subject, float deltaTime);
      public void Cancel(bool isKeyHeld); public void Reset();
  }
  ```

- [ ] **Step 1: テストを書く**

```csharp
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteHoldConfirmTest
    {
        private static readonly DeleteHoldSubject SelectionRev1 = DeleteHoldSubject.Selection(1);

        [Test]
        public void 押し続けると進捗が伸び2秒で満了する()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsFalse(hold.Advance(true, SelectionRev1, 1f));
            Assert.IsTrue(bar.IsShown);
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            Assert.IsTrue(hold.Advance(true, SelectionRev1, 1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void 途中で離すと進捗が0に戻りバーが消える()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, SelectionRev1, 1f);
            Assert.IsFalse(hold.Advance(false, SelectionRev1, 0.1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.Advance(true, SelectionRev1, 1.5f));
            Assert.AreEqual(0.75f, bar.CurrentProgress, 0.001f);
        }

        [Test]
        public void 対象が変わると進捗が0からやり直しになる()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, DeleteHoldSubject.Selection(1), 1.5f);
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.Selection(2), 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            var key = new object();
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.Hovered(key), 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            Assert.IsTrue(hold.Advance(true, DeleteHoldSubject.Hovered(key), 1f), "the same hovered key continues the hold");
        }

        [Test]
        public void 対象が無いときはバーを出さない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.None, 1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void 満了後は一度離すまで次の長押しを始めない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsTrue(hold.Advance(true, SelectionRev1, 2f));
            Assert.IsFalse(hold.Advance(true, DeleteHoldSubject.Selection(2), 5f));
            Assert.IsFalse(bar.IsShown);
            hold.Advance(false, DeleteHoldSubject.Selection(2), 0.1f);
            Assert.IsTrue(hold.Advance(true, DeleteHoldSubject.Selection(2), 2f));
        }

        [Test]
        public void Resetで進捗とバーを戻す()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, SelectionRev1, 1f);
            hold.Reset();
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }
    }
}
```

- [ ] **Step 2: DeleteHoldSubject を書く**

```csharp
using System;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
{
    public enum DeleteHoldSubjectKind
    {
        None,
        Selection,
        Hovered,
    }

    /// <summary>
    ///     F長押しの対象。対象なし／選択集合（Revision で同一性）／ホバー1件（論理キーで同一性）を判別子で表す
    ///     The hold-to-confirm subject: none, the selection (identified by revision) or one hovered target (identified by its logical key)
    /// </summary>
    public readonly struct DeleteHoldSubject : IEquatable<DeleteHoldSubject>
    {
        public static DeleteHoldSubject None => new(DeleteHoldSubjectKind.None, null);
        public static DeleteHoldSubject Selection(int revision) => new(DeleteHoldSubjectKind.Selection, revision);
        public static DeleteHoldSubject Hovered(object targetKey) => new(DeleteHoldSubjectKind.Hovered, targetKey);

        public DeleteHoldSubjectKind Kind { get; }
        public bool IsNone => Kind == DeleteHoldSubjectKind.None;
        private readonly object _key;

        private DeleteHoldSubject(DeleteHoldSubjectKind kind, object key)
        {
            Kind = kind;
            _key = key;
        }

        public bool Equals(DeleteHoldSubject other)
        {
            return Kind == other.Kind && Equals(_key, other._key);
        }

        public override bool Equals(object obj)
        {
            return obj is DeleteHoldSubject other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, _key);
        }
    }
}
```

- [ ] **Step 3: DeleteHoldConfirm を書く**

```csharp
using Client.Game.InGame.UI.ProgressBar;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
{
    /// <summary>
    ///     F長押しによる削除確定の進捗。件数に関係なく一定時間で満了し、採掘と同じ進捗バーへ流す
    ///     Hold-to-confirm progress for deletion: a fixed duration regardless of count, shown on the mining progress bar
    /// </summary>
    public class DeleteHoldConfirm
    {
        public const float HoldSeconds = 2f;

        private readonly ProgressBarState _progressBar;
        private DeleteHoldSubject _subject = DeleteHoldSubject.None;
        private float _heldSeconds;
        private bool _awaitRelease;

        public bool IsHolding { get; private set; }

        public DeleteHoldConfirm(ProgressBarState progressBar)
        {
            _progressBar = progressBar;
        }

        // 毎フレーム呼ぶ。満了したフレームだけ true。離す・対象が変わる・対象なしで進捗は0へ戻る
        // Call every frame; true only on the frame the hold completes. Release, a changed subject, or no subject resets progress
        public bool Advance(bool isKeyHeld, DeleteHoldSubject subject, float deltaTime)
        {
            // 満了直後に押し続けても次の対象へ連続確定しない
            // Keeping the key down after completion must not chain into the next subject
            if (!isKeyHeld) _awaitRelease = false;
            if (!isKeyHeld || subject.IsNone || _awaitRelease)
            {
                Reset();
                return false;
            }

            if (!IsHolding || !_subject.Equals(subject)) Begin(subject);

            _heldSeconds += deltaTime;
            _progressBar.SetProgress(_heldSeconds / HoldSeconds);
            if (_heldSeconds < HoldSeconds) return false;

            Reset();
            _awaitRelease = true;
            return true;
        }

        // 取りやめた押下から別の対象への確定を連鎖させない
        // A cancelled key hold must not chain into confirmation of another target
        public void Cancel(bool isKeyHeld)
        {
            Reset();
            _awaitRelease = isKeyHeld;
        }

        public void Reset()
        {
            if (IsHolding) _progressBar.Hide();
            if (_heldSeconds > 0f) _progressBar.SetProgress(0f);
            IsHolding = false;
            _subject = DeleteHoldSubject.None;
            _heldSeconds = 0f;
        }

        private void Begin(DeleteHoldSubject subject)
        {
            if (!IsHolding) _progressBar.Show();
            IsHolding = true;
            _subject = subject;
            _heldSeconds = 0f;
        }
    }
}
```

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.DragDelete.DeleteHoldConfirmTest"`
Expected: ErrorCount 0 / 6 PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs
git commit -m "feat: 削除ツールのF長押し確定（2秒・進捗バー・判別子付き対象）を足す"
```

---

### Task 5: 箱選択（DeleteBoxSelection・DeleteBoxTargetResolver・DeleteBoxInput）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs`
- Modify: `Localization/localization.csv`（`ui.tooltip.blueprintCopyEmptyRange` 行の直後に 3 行追加）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`npm run gen:i18n` で再生成）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs`

**Interfaces:**
- Consumes: `CellRangeBox`, `CellRangeBoxVisualizer`（Task 1）、`DeleteBoxRule`, `DeleteBoxRailSampler`, `TrainRailObjectManager.EnumerateRailChains()`, `TrainCarObjectDatastore.EnumerateTrainCars()`（Task 2）、`DeleteSelection.AddBoxTargets`（Task 3）、`BlueprintCopyTargetRule.CreateBox(Vector3Int, Vector3Int)`（`Game.Blueprint`）、`BlockPositionInfo.IsOverlap`（拡張メソッド。`using Game.Block.Interface.Extension;`）、`PlacementUnitCellResolver.TryGetCursorCell(Camera, int, out Vector3Int)`、`PlacementHeightOffset`（DI シングルトン。`Adjust`/`ResetToGround`/`Value`）、`PlacementHeightKeyInput.Apply(PlacementHeightOffset)`、`UiPointerHitTest.IsPointerOverAnyUi()`、`InputManager.Playable.ScreenLeftClick`、`HybridInput.GetKey(KeyCode.LeftShift)`、`BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary`、`BlockGameObject.BlockPosInfo`、`BlockGameObjectChild`、`TrainCarEntityObject`、`TrainCarEntityChildrenObject`、`TooltipLine(LocalizationKey, string[])`
- Produces:
  ```csharp
  public enum DeleteBoxPhase { Idle, SelectingEnd }
  public class DeleteBoxSelection { public DeleteBoxPhase Phase { get; } public Vector3Int StartCell { get; } public void SelectStart(Vector3Int cell); public void Clear(); }
  public class DeleteBoxTargetResolver
  {
      public DeleteBoxTargetResolver(BlockGameObjectDataStore blockGameObjectDataStore, TrainCarObjectDatastore trainCarObjectDatastore);
      public IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max);   // 撤去可能な対象だけの新しい List。撤去不可は件数と理由をログ
  }
  internal enum DeleteBoxClick { None, Confirmed, ReleasedOverUi }
  public class DeleteBoxInput
  {
      public DeleteBoxInput(Camera mainCamera, DeleteBoxTargetResolver resolver, PlacementHeightOffset heightOffset);
      public bool IsSelectingEnd { get; }
      public bool ClaimsLeftPress();                                       // 終点待ち中か Shift 押下中。true の間は左押下は箱の物（なぞりは始めない）
      public void Update(DeleteSelection selection, List<TooltipLine> tooltipLines, bool isPointerOverUi);   // 毎フレーム。終点確定のフレームに selection.AddBoxTargets を呼ぶ
      public bool TryCancel();                                             // 終点待ちを畳めたら true
      public void Clear();                                                 // 局面・高さ（ResetToGround）・表示を全て戻す
  }
  ```
- ローカライズ追加キー: `ui.tooltip.deleteBoxTargetsInRange`（`Targets in range: {p0}` / `範囲内の対象: {p0}`）、`ui.tooltip.deleteBoxEmptyRange`（`No targets in range` / `範囲に対象がありません`）、`ui.tooltip.deleteSelectedCount`（`Selected: {p0}` / `選択中: {p0}件`）

- [ ] **Step 1: テストを書く**

```csharp
using System;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteBoxSelectionTest
    {
        [Test]
        public void 始点を選ぶと終点待ちになりClearで戻る()
        {
            var selection = new DeleteBoxSelection();
            Assert.AreEqual(DeleteBoxPhase.Idle, selection.Phase);
            selection.SelectStart(new Vector3Int(1, 2, 3));
            Assert.AreEqual(DeleteBoxPhase.SelectingEnd, selection.Phase);
            Assert.AreEqual(new Vector3Int(1, 2, 3), selection.StartCell);
            selection.Clear();
            Assert.AreEqual(DeleteBoxPhase.Idle, selection.Phase);
        }

        [Test]
        public void 終点待ち中にもう一度始点は選べない()
        {
            var selection = new DeleteBoxSelection();
            selection.SelectStart(Vector3Int.zero);
            Assert.Throws<InvalidOperationException>(() => selection.SelectStart(Vector3Int.one));
        }
    }
}
```

- [ ] **Step 2: ローカライズキーを足す**

`Localization/localization.csv` の `ui.tooltip.blueprintCopyEmptyRange` 行の直後に（列順は既存行と同じ: key, Source, English, 日本語, Deutsch, 한국어）:

```
ui.tooltip.deleteBoxTargetsInRange,Targets in range: {p0},Targets in range: {p0},範囲内の対象: {p0},Ziele im Bereich: {p0},범위 내 대상: {p0}
ui.tooltip.deleteBoxEmptyRange,No targets in range,No targets in range,範囲に対象がありません,Keine Ziele im Bereich,범위에 대상이 없습니다
ui.tooltip.deleteSelectedCount,Selected: {p0},Selected: {p0},選択中: {p0}件,Ausgewählt: {p0},선택 중: {p0}개
```

```bash
cd moorestech_web/webui && npm run gen:i18n && cd ../..
uloop compile --project-path ./moorestech_client --force-recompile
uloop compile --project-path ./moorestech_client
```

- [ ] **Step 3: DeleteBoxSelection を書く**

```csharp
using System;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    public enum DeleteBoxPhase
    {
        Idle,
        SelectingEnd,
    }

    /// <summary>
    ///     箱選択の局面と始点セル。終点は確定の瞬間に対象へ解決されるので保持しない（BlueprintCopySelection と同形）
    ///     Box-selection phase and its start cell; the end cell resolves into targets at once, so it is not stored (same shape as BlueprintCopySelection)
    /// </summary>
    public class DeleteBoxSelection
    {
        public DeleteBoxPhase Phase { get; private set; } = DeleteBoxPhase.Idle;
        public Vector3Int StartCell { get; private set; }

        public void SelectStart(Vector3Int cell)
        {
            if (Phase != DeleteBoxPhase.Idle) throw new InvalidOperationException($"SelectStart in {Phase}");
            StartCell = cell;
            Phase = DeleteBoxPhase.SelectingEnd;
        }

        public void Clear()
        {
            Phase = DeleteBoxPhase.Idle;
        }
    }
}
```

- [ ] **Step 4: DeleteBoxTargetResolver を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.View.Object.Core;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     AABBに入る撤去可能な削除対象（ブロック・レール edge・列車車両）の列を返す1本の問い合わせ。撤去不可は件数と理由をログして除く。接続線は端点ブロックの撤去に巻き込まれるので含めない
    ///     One query returning the removable delete targets (blocks, rail edges, train cars) inside an AABB; non-removable ones are logged and dropped, connection lines ride on their endpoint blocks
    /// </summary>
    public class DeleteBoxTargetResolver
    {
        private readonly HashSet<string> _reportedDiagnostics = new();
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;

        public DeleteBoxTargetResolver(BlockGameObjectDataStore blockGameObjectDataStore, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        // 箱操作ごとに診断を一度だけ出し、次の箱では再び報告する
        // Report each diagnostic once per box operation, then allow it for the next box
        internal void ClearDiagnostics() => _reportedDiagnostics.Clear();

        public IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max)
        {
            var targets = new List<IDeleteTarget>();
            var newlyReportedSkipped = 0;
            // ブロックの箱判定はBPコピーと同じ箱とIsOverlapを使う（複製しない）
            // Blocks use the same box and IsOverlap as blueprint copy (no duplicate rule)
            var box = BlueprintCopyTargetRule.CreateBox(min, max);
            CollectBlocks();
            CollectRails();
            CollectTrainCars();
            if (newlyReportedSkipped > 0) Debug.Log($"[DeleteBox] {newlyReportedSkipped} newly reported non-removable target(s) excluded from {min}-{max}");
            return targets;

            #region Internal

            void CollectBlocks()
            {
                foreach (var block in _blockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary.Values)
                {
                    if (!block.BlockPosInfo.IsOverlap(box)) continue;
                    var child = block.GetComponentInChildren<BlockGameObjectChild>(true);
                    if (child == null)
                    {
                        LogWarningOnce($"[DeleteBox] block has no BlockGameObjectChild: {block.BlockPosInfo.OriginalPos}");
                        continue;
                    }
                    AddIfRemovable(child);
                }
            }

            void CollectRails()
            {
                foreach (var chain in TrainRailObjectManager.Instance.EnumerateRailChains())
                {
                    if (chain.IsRemoving) continue;
                    var mesh = chain.GetComponentInChildren<BezierRailMesh>();
                    if (mesh == null)
                    {
                        LogWarningOnce($"[DeleteBox] rail chain has no BezierRailMesh: {chain.name}");
                        continue;
                    }
                    if (!DeleteBoxRailSampler.IntersectsCellBox(mesh, min, max)) continue;
                    var rail = chain.GetComponentInChildren<DeleteTargetRail>(true);
                    if (rail == null)
                    {
                        LogWarningOnce($"[DeleteBox] rail chain has no DeleteTargetRail: {chain.name}");
                        continue;
                    }
                    AddIfRemovable(rail);
                }
            }

            void CollectTrainCars()
            {
                foreach (var car in _trainCarObjectDatastore.EnumerateTrainCars())
                {
                    if (!DeleteBoxRule.IsPointInBox(car.transform.position, min, max)) continue;
                    var child = car.GetComponentInChildren<TrainCarEntityChildrenObject>(true);
                    if (child == null)
                    {
                        LogWarningOnce($"[DeleteBox] train car has no TrainCarEntityChildrenObject: {car.TrainCarInstanceId}");
                        continue;
                    }
                    AddIfRemovable(child);
                }
            }

            // 撤去不可は理由をログして除く（表示件数・0件拒否・追加が同じ列を使う）
            // Drop non-removable targets with their reason logged so count, empty-check and add share one list
            void AddIfRemovable(IDeleteTarget target)
            {
                if (target.IsRemovable(out var denyReason))
                {
                    targets.Add(target);
                    return;
                }
                var diagnostic = $"[DeleteBox] skipped non-removable target key={target.GetDeleteTargetKey()} reason={denyReason?.Key ?? "(none)"}";
                // silent-ok: 同一箱で報告済みの理由だけ抑止 / 最初の検出時に開発者ログへ出力済み
                if (!_reportedDiagnostics.Add(diagnostic)) return;
                newlyReportedSkipped++;
                Debug.Log(diagnostic);
            }

            void LogWarningOnce(string diagnostic)
            {
                // silent-ok: 同一箱で報告済みの欠落だけ抑止 / 最初の検出時に開発者警告へ出力済み
                if (_reportedDiagnostics.Add(diagnostic)) Debug.LogWarning(diagnostic);
            }

            #endregion
        }
    }
}
```

- [ ] **Step 5: DeleteBoxInput を書く**

```csharp
using System.Collections.Generic;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Tooltip;
using Client.Input;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    internal enum DeleteBoxClick
    {
        None,
        Confirmed,
        ReleasedOverUi,
    }

    /// <summary>
    ///     Shift+左クリックで始点、次の左クリックで終点を取る箱選択の入力・セル解決・可視化。終点確定時に選択へ直接足す。高さは設置モードと共用のオフセット
    ///     Box-selection input (Shift+click start, click end), cell resolution and visuals; adds to the selection directly on end confirm. Height shares the placement offset
    /// </summary>
    public class DeleteBoxInput
    {
        private readonly Camera _mainCamera;
        private readonly DeleteBoxTargetResolver _resolver;
        private readonly PlacementHeightOffset _heightOffset;
        private readonly DeleteBoxSelection _selection = new();
        private CellRangeBoxVisualizer _visualizer;
        private bool _isPressRegistered;

        public bool IsSelectingEnd => _selection.Phase == DeleteBoxPhase.SelectingEnd;

        public DeleteBoxInput(Camera mainCamera, DeleteBoxTargetResolver resolver, PlacementHeightOffset heightOffset)
        {
            _mainCamera = mainCamera;
            _resolver = resolver;
            _heightOffset = heightOffset;
        }

        // 終点待ち中かShift押下中は左押下は箱の物（この間なぞりは始まらない）
        // While awaiting the end point or holding Shift, the left press belongs to the box (no stroke starts)
        public bool ClaimsLeftPress()
        {
            return IsSelectingEnd || HybridInput.GetKey(KeyCode.LeftShift);
        }

        public void Update(DeleteSelection deleteSelection, List<TooltipLine> tooltipLines, bool isPointerOverUi)
        {
            PlacementHeightKeyInput.Apply(_heightOffset);
            var isShiftHeld = HybridInput.GetKey(KeyCode.LeftShift);
            var click = ConsumeClick();
            if (click == DeleteBoxClick.ReleasedOverUi) Debug.Log("[DeleteBox] click ignored: released over UI");
            var isClicked = click == DeleteBoxClick.Confirmed;

            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorCell))
            {
                if (isClicked) Debug.Log("[DeleteBox] click refused: cursor has no placement surface");
                if (IsSelectingEnd) ShowStart(_selection.StartCell);
                else _visualizer?.HideAll();
                return;
            }

            if (IsSelectingEnd) UpdateSelectingEnd();
            else UpdateIdle();

            #region Internal

            // 箱の物である押下（ClaimsLeftPress）→UI外の解放を1クリックに畳む。UI上の解放は理由付きで捨てる
            // Fold a press the box owns (ClaimsLeftPress) and a release outside UI into one click; a release over UI is dropped with a reason
            DeleteBoxClick ConsumeClick()
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !isPointerOverUi && ClaimsLeftPress()) _isPressRegistered = true;
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || !_isPressRegistered) return DeleteBoxClick.None;
                _isPressRegistered = false;
                return isPointerOverUi ? DeleteBoxClick.ReleasedOverUi : DeleteBoxClick.Confirmed;
            }

            void UpdateIdle()
            {
                // Shiftを押している間だけ始点候補を示す
                // Show the start candidate only while Shift is held
                if (isShiftHeld) ShowStart(cursorCell);
                else _visualizer?.HideAll();
                if (isClicked) _selection.SelectStart(cursorCell);
            }

            void UpdateSelectingEnd()
            {
                var (min, max) = CellRangeBox.Calc(_selection.StartCell, cursorCell);
                var targets = _resolver.Resolve(min, max);
                tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange, new[] { targets.Count.ToString() }));
                if (targets.Count == 0) tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange));
                GetVisualizer().ShowSelectingEnd(_selection.StartCell, cursorCell, min, max);
                if (!isClicked) return;

                // 空範囲は確定を拒み、ツールチップとログへ理由を残す
                // Refuse an empty range, leaving its reason in tooltip and log
                if (targets.Count == 0)
                {
                    Debug.Log($"[DeleteBox] end click refused: no removable targets in {min}-{max}");
                    return;
                }
                var added = deleteSelection.AddBoxTargets(targets);
                Debug.Log($"[DeleteBox] added={added} of {targets.Count} selected={deleteSelection.Count}");
                Clear();
            }

            // 始点を実際に表示するときだけ可視化オブジェクトを作る
            // Create the visualizer only when the start marker is actually shown
            void ShowStart(Vector3Int cell)
            {
                GetVisualizer().ShowSelectingStart(cell);
            }

            CellRangeBoxVisualizer GetVisualizer()
            {
                return _visualizer ??= new CellRangeBoxVisualizer("DeleteBox", MaterialConst.NotPlaceableColor);
            }

            #endregion
        }

        // 終点待ちなら箱を畳んで true（ESCの1段目）
        // Fold the pending box and return true (first ESC stage)
        public bool TryCancel()
        {
            if (!IsSelectingEnd) return false;
            Clear();
            return true;
        }

        // 局面・高さ・押下・表示を戻す（箱を畳む・確定後・モード離脱）
        // Reset phase, height, press and visuals (fold, after confirm, mode exit)
        public void Clear()
        {
            _selection.Clear();
            _resolver.ClearDiagnostics();
            _heightOffset.ResetToGround();
            _isPressRegistered = false;
            _visualizer?.HideAll();
        }
    }
}
```

（`UiPointerHitTest` は `Client.Game.InGame.Control`、`PlacementHeightKeyInput` は `Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height`。実ファイルの `namespace` に合わせて `using` を調整する）

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.DragDelete.DeleteBoxSelectionTest"`
Expected: ErrorCount 0 / 2 PASS。`DeleteBoxInput.cs` が 200 行以下であることを確認。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat: 削除ツールの箱選択（Shift+クリック2点・Q/E高さ・箱内対象の解決と追加）を足す"
```

---

### Task 6: DeleteObjectService の司令塔化・DeleteObjectState 配線・キーヒント

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs:18-33,57-63,120-132`
- Modify: `Localization/localization.csv`（`ui.keyHint.key.leftAltHold` 行の直後に `ui.keyHint.key.fHold`、`ui.keyHint.text.dragSelect` 行の直後に 4 行）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（再生成）
- Modify: `docs/adr/0032-key-hint-hud-owned-by-ui-state.md:52`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs`（破壊モード 7→11）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateFocusRestorationTest.cs:119`、`.../UIStateCameraInteractionTest.cs:92`、`.../RightShortPressTransitionTest.cs:68`（コンストラクタ引数追加）

**Interfaces:**
- Consumes: Task 3〜5 の `DeleteSelection`・`DeleteAddOutcome`・`DeleteHoldSubject`・`DeleteHoldConfirm`・`DeleteBoxInput`・`DeleteBoxTargetResolver`、`DeleteTargetRaycaster.AimAt(DeleteAimFilter)` → `DeleteAimResult`（`Target`、`GetDragDenyReason()`）、`IMouseCursorTooltip.Show(TooltipOwner, IReadOnlyList<TooltipLine>)`/`Hide(TooltipOwner)`、`InputManager.Playable.Interact.GetKey`
- Produces:
  ```csharp
  public class DeleteObjectService
  {
      public DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender, ProgressBarState progressBar, Camera mainCamera, PlacementHeightOffset heightOffset, DeleteBoxTargetResolver boxTargetResolver);
      public void Update(float deltaTime);
      public bool TryCancel();        // 長押しを戻し、箱 → 選択 の順に1段だけ畳む。畳む物が無ければ false
      public void ClearAll();         // モード離脱: 選択・箱（高さ含む）・長押し・ツールチップを全て片付ける
  }
  ```
  `DeleteObjectState(IRemovalRestoreSender, UiStateCameraPolicyService, BuildOperationHistory, BuildUndoService, PlacementTargetPickService, RightShortPressInputService, IMouseCursorTooltip, ProgressBarState, Camera, PlacementHeightOffset, BlockGameObjectDataStore, TrainCarObjectDatastore)`
- ローカライズ追加キー: `ui.keyHint.key.fHold`（`F (Hold)` / `F（長押し）` / `F (halten)` / `F (길게 누르기)`）、`ui.keyHint.text.boxSelect`（`Box Select` / `範囲選択` / `Bereich auswählen` / `범위 선택`）、`ui.keyHint.text.lowerBoxHeight`（`Lower Range Height` / `範囲の高さを下げる` / `Bereichshöhe senken` / `범위 높이 낮추기`）、`ui.keyHint.text.raiseBoxHeight`（`Raise Range Height` / `範囲の高さを上げる` / `Bereichshöhe erhöhen` / `범위 높이 높이기`）、`ui.keyHint.text.deleteSelected`（`Delete Selected` / `選択を削除` / `Auswahl entfernen` / `선택 삭제`）

- [ ] **Step 1: ローカライズキーを足して再生成する**

`Localization/localization.csv`:

```
ui.keyHint.key.fHold,F (Hold),F (Hold),F（長押し）,F (halten),F (길게 누르기)
ui.keyHint.text.boxSelect,Box Select,Box Select,範囲選択,Bereich auswählen,범위 선택
ui.keyHint.text.lowerBoxHeight,Lower Range Height,Lower Range Height,範囲の高さを下げる,Bereichshöhe senken,범위 높이 낮추기
ui.keyHint.text.raiseBoxHeight,Raise Range Height,Raise Range Height,範囲の高さを上げる,Bereichshöhe erhöhen,범위 높이 높이기
ui.keyHint.text.deleteSelected,Delete Selected,Delete Selected,選択を削除,Auswahl entfernen,선택 삭제
```

```bash
cd moorestech_web/webui && npm run gen:i18n && cd ../..
uloop compile --project-path ./moorestech_client --force-recompile
uloop compile --project-path ./moorestech_client
```

- [ ] **Step 2: キーヒントのテストを更新する**

`UIStateKeyHintCatalogTest.cs` の `EveryScreenHintCountMatchesAdr` の `Assert.AreEqual(7, DeleteObjectStateHints.Hints.Count);` を `11` へ。さらに次のテストを追加:

```csharp
        [Test]
        public void DeleteObjectHintsMatchAdr()
        {
            var expected = new[]
            {
                (LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DragSelect),
                (LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BoxSelect),
                (LocalizationKeys.Ui.KeyHint.Key.Q, LocalizationKeys.Ui.KeyHint.Text.LowerBoxHeight),
                (LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.RaiseBoxHeight),
                (LocalizationKeys.Ui.KeyHint.Key.FHold, LocalizationKeys.Ui.KeyHint.Text.DeleteSelected),
                (LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.ExitDeleteMode),
                (LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
                (LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
                (LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
                (LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Text.Undo),
                (LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
            };
            AssertHints(expected, DeleteObjectStateHints.Hints);
        }
```

`docs/adr/0032-key-hint-hud-owned-by-ui-state.md:52` の破壊モード行を次へ:

```
| 破壊モード | 左ドラッグ まとめて選択／Shift+左クリック 範囲選択／Q 範囲の高さを下げる／E 範囲の高さを上げる／F（長押し） 選択を削除／G 破壊モード終了／B ビルドメニュー／Tab インベントリ／中クリック 設置物をスポイト／Ctrl+Z 元に戻す／V 視点切替 |
```

- [ ] **Step 3: DeleteObjectService を書き換える（全文）**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.ProgressBar;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Input;
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 破壊モードの削除インタラクション（箱・なぞり・ホバー・F長押し確定・ツールチップ）を毎フレーム束ねる司令塔
    // Per-frame conductor of destroy-mode interaction (box, stroke, hover, F hold-to-confirm, tooltip)
    public class DeleteObjectService
    {
        private readonly DeleteSelection _selection;
        private readonly DeleteBoxInput _box;
        private readonly DeleteHoldConfirm _hold;
        private readonly IMouseCursorTooltip _tooltip;
        private readonly TooltipOwner _tooltipOwner = new();
        private readonly List<TooltipLine> _lines = new();
        private bool _isStroking;

        public DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender, ProgressBarState progressBar, Camera mainCamera, PlacementHeightOffset heightOffset, DeleteBoxTargetResolver boxTargetResolver)
        {
            _selection = new DeleteSelection(buildOperationHistory, restoreSender);
            _box = new DeleteBoxInput(mainCamera, boxTargetResolver, heightOffset);
            _hold = new DeleteHoldConfirm(progressBar);
            _tooltip = tooltip;
        }

        public void Update(float deltaTime)
        {
            _lines.Clear();

            // 固定カテゴリー内の最前面を取る（ストローク外は最前面）
            // Resolve the frontmost target in the stroke category, or overall outside a stroke
            var aim = DeleteTargetRaycaster.AimAt(_selection.AimFilter);
            var hovered = aim.Target;
            var isPointerOverUi = UiPointerHitTest.IsPointerOverAnyUi();

            // 箱は終点確定のフレームに選択へ直接足す
            // The box adds to the selection itself on the frame the end point is confirmed
            _box.Update(_selection, _lines, isPointerOverUi);

            HandleStrokeStart();
            if (_isStroking) UpdateStroke();
            else UpdateHoverPreview();
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp) EndStroke();

            UpdateHoldConfirm();

            if (_selection.HasSelection()) _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount, new[] { _selection.Count.ToString() }));
            if (_lines.Count == 0) _tooltip.Hide(_tooltipOwner);
            else _tooltip.Show(_tooltipOwner, _lines.ToArray());

            #region Internal

            // 箱の物である押下（終点待ち・Shift）ではなぞりを始めない
            // A press the box owns (awaiting end / Shift) never starts a stroke
            void HandleStrokeStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (isPointerOverUi) return;
                if (_box.ClaimsLeftPress()) return;

                // ホバーの赤は選択側が引き継ぐ（追加時に再び赤になる）
                // The selection takes over the hover red (adding re-reds it)
                _selection.SetHoverPreview(null);
                _selection.BeginStroke();
                _isStroking = true;
            }

            void UpdateStroke()
            {
                if (!_selection.IsStrokeAlive()) return;
                if (hovered == null)
                {
                    AddDenyLine(aim.GetDragDenyReason());
                    return;
                }
                var outcome = _selection.TryAddTarget(hovered);
                if (!outcome.IsAdded) AddDenyLine(outcome.DenyReason);
            }

            // 離した時点でカテゴリー固定を最前面へ戻す（選択は残る）
            // Releasing the button returns the category fix to frontmost (the selection stays)
            void EndStroke()
            {
                _isStroking = false;
                _selection.EndStroke();
            }

            void UpdateHoverPreview()
            {
                _selection.SetHoverPreview(hovered);
                // 削除不可な対象は理由行だけ積む（理由なし拒否は何も出さない）
                // For a non-removable target only add the denial line (a reasonless denial adds nothing)
                if (hovered != null && !hovered.IsRemovable(out var reason)) AddDenyLine(reason);
            }

            // 長押しの対象: 選択があれば集合（Revision）、無ければ撤去可能なホバー1件、どちらも無ければ None
            // Hold subject: the selection (by revision) when present, else the single removable hovered target, else None
            void UpdateHoldConfirm()
            {
                var subject = DeleteHoldSubject.None;
                var hoverTarget = _selection.HoverPreview;
                if (_selection.HasSelection()) subject = DeleteHoldSubject.Selection(_selection.Revision);
                else if (hoverTarget != null && hoverTarget.IsRemovable(out _)) subject = DeleteHoldSubject.Hovered(hoverTarget.GetDeleteTargetKey());

                if (!_hold.Advance(InputManager.Playable.Interact.GetKey, subject, deltaTime)) return;

                // ホバー1件の暗黙選択: 足せなければ（撤去可否が直前に変わった）確定しない
                // Implicit single selection: if it cannot join (removability changed meanwhile) nothing is committed
                if (subject.Kind == DeleteHoldSubjectKind.Hovered)
                {
                    _selection.BeginStroke();
                    var outcome = _selection.TryAddTarget(hoverTarget);
                    if (!outcome.IsAdded)
                    {
                        Debug.Log($"[DragDelete] implicit hover commit refused: {outcome.Kind}");
                        _selection.SetHoverPreview(null);
                        return;
                    }
                }
                _selection.CommitDelete();
                _isStroking = false;
            }

            void AddDenyLine(LocalizationKey? denyReasonKey)
            {
                if (!denyReasonKey.HasValue) return;
                _lines.Add(new TooltipLine(denyReasonKey.Value));
            }

            #endregion
        }

        // ESC/右短押し: 長押しを戻し、終点待ちの箱 → 選択の全解除 の順に1段だけ畳む。畳む物が無ければ false（呼び出し側がモード終了を判断）
        // ESC / right short press: reset the hold, then fold the pending box, else clear the selection; false when nothing to fold (caller decides to exit)
        public bool TryCancel()
        {
            _hold.Cancel(InputManager.Playable.Interact.GetKey);
            if (_box.TryCancel()) return true;
            if (!_selection.HasSelection()) return false;
            _selection.CancelSelection();
            return true;
        }

        // モード離脱: 選択・箱（高さ含む）・長押し・ツールチップを全て片付ける
        // Mode exit: clear selection, box (including height), hold and tooltip
        public void ClearAll()
        {
            _selection.CancelSelection();
            _box.Clear();
            _hold.Reset();
            _tooltip.Hide(_tooltipOwner);
            _isStroking = false;
        }
    }
}
```

- [ ] **Step 4: DeleteObjectState を配線する**

コンストラクタ:

```csharp
        public DeleteObjectState(IRemovalRestoreSender restoreSender, UiStateCameraPolicyService cameraPolicyService, BuildOperationHistory buildOperationHistory, BuildUndoService buildUndoService, PlacementTargetPickService placementTargetPickService, RightShortPressInputService rightShortPressInputService, IMouseCursorTooltip tooltip, ProgressBarState progressBar, Camera mainCamera, PlacementHeightOffset heightOffset, BlockGameObjectDataStore blockGameObjectDataStore, TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _cameraPolicyService = cameraPolicyService;
            var boxTargetResolver = new DeleteBoxTargetResolver(blockGameObjectDataStore, trainCarObjectDatastore);
            _deleteObjectService = new DeleteObjectService(buildOperationHistory, tooltip, restoreSender, progressBar, mainCamera, heightOffset, boxTargetResolver);
            _buildUndoService = buildUndoService;
            _placementTargetPickService = placementTargetPickService;
            _rightShortPressInputService = rightShortPressInputService;
        }
```

`GetNextUpdate` 内: `_deleteObjectService.Update();` → `_deleteObjectService.Update(Time.deltaTime);`、`HandleTransition` 内の `!_deleteObjectService.TryCancelSelection()` → `!_deleteObjectService.TryCancel()`、ESC コメントを「ESC/右短押しはまず終点待ちの箱、次に選択の全解除に使い、どちらも無ければ破壊モードを抜ける」へ。`OnExit` の `_deleteObjectService.CancelSelection();` → `_deleteObjectService.ClearAll();`。`using Client.Game.InGame.Block;`、`using Client.Game.InGame.BlockSystem.PlaceSystem.Common;`、`using Client.Game.InGame.Train.View.Object.Core;`、`using Client.Game.InGame.UI.ProgressBar;`、`using Client.Game.InGame.UI.UIState.State.DragDelete.Box;` を足す。`PlacementHeightOffset`・`Camera`・`TrainCarObjectDatastore` は DI 登録済み（`MainGameInteractionRegistration.cs:93`、`MainGameStarter.cs:123`、`MainGameStarter.cs:64`）。

ヒント:

```csharp
    internal static class DeleteObjectStateHints
    {
        public static readonly IReadOnlyList<KeyHint> Hints = new[]
        {
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.LeftDrag, LocalizationKeys.Ui.KeyHint.Text.DragSelect),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.ShiftLeftClick, LocalizationKeys.Ui.KeyHint.Text.BoxSelect),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Q, LocalizationKeys.Ui.KeyHint.Text.LowerBoxHeight),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.E, LocalizationKeys.Ui.KeyHint.Text.RaiseBoxHeight),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.FHold, LocalizationKeys.Ui.KeyHint.Text.DeleteSelected),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.G, LocalizationKeys.Ui.KeyHint.Text.ExitDeleteMode),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.B, LocalizationKeys.Ui.KeyHint.Text.BuildMenu),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.Tab, LocalizationKeys.Ui.KeyHint.Text.Inventory),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.MiddleClick, LocalizationKeys.Ui.KeyHint.Text.PickPlacedObject),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.CtrlZ, LocalizationKeys.Ui.KeyHint.Text.Undo),
            new KeyHint(LocalizationKeys.Ui.KeyHint.Key.V, LocalizationKeys.Ui.KeyHint.Text.ToggleView),
        };
    }
```

- [ ] **Step 5: 既存テストのコンストラクタ呼び出しを更新する**

`UIStateFocusRestorationTest.cs:119`、`UIStateCameraInteractionTest.cs:92`、`RightShortPressTransitionTest.cs:68` の `new DeleteObjectState(..., <tooltip>)` の末尾に `, new ProgressBarState(), CreateObject("DeleteTestCamera", true).AddComponent<Camera>(), new PlacementHeightOffset(), null, null` を足す（`BlockGameObjectDataStore`・`TrainCarObjectDatastore` は箱の終点待ち中にしか触られないので null でよい。`CreateObject` は `UIStateTestFixtureBase` の既存ヘルパー。無いフィクスチャでは `new GameObject("DeleteTestCamera").AddComponent<Camera>()` を使い TearDown で `Object.DestroyImmediate`）。`using Client.Game.InGame.UI.ProgressBar;`・`using Client.Game.InGame.BlockSystem.PlaceSystem.Common;` を足す。

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.UIState"`
Expected: ErrorCount 0 / 全 PASS。`DeleteObjectService.cs`・`DeleteObjectState.cs` が 200 行以下。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State moorestech_client/Assets/Scripts/Client.Tests/UIState Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts docs/adr/0032-key-hint-hud-owned-by-ui-state.md
git commit -m "feat: 削除ツールを選択積み上げ→F長押し確定へ変え、箱選択とキーヒントを配線する"
```

---

### Task 7: unityプレイ録画テスト（新規シナリオ＋既存 3 シナリオの更新）

**Files:**
- Create: `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool-box-select-hold-confirm.cs`
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs`（削除手順）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/train/delete-tool-pier-removal-undo-rail.cs`（削除手順＋レールを跨ぐ箱の 1 ステップ）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs`（`ClickLine` と Step 3 のドラッグ撤去の後に F 長押し）

**Interfaces:**
- Consumes: プレイテスト DSL（`PlaytestRunner.Run`、`p.SetupDebugEnvironment`、`p.WarpPlayer`、`p.PressKey(Key)`、`p.WaitUiState`、`p.AimAt(Vector3)`、`p.Until`、`p.WaitSeconds`、`p.Note`、`p.Assert`、`p.Screenshot`、`p.GetBlock`、`PlaytestBlockOps.PlaceBlockDirect`、`SemanticInput.KeyDown/KeyUp/MouseButtonDown/MouseButtonUp`）、`ClientDIContext.BlockGameObjectDataStore`、`MouseCursorTooltipState.GetPresentation()`、`DeleteHoldConfirm.HoldSeconds`
- Produces: `result.json` 全 PASS の録画。既存 3 シナリオも PASS のまま

- [ ] **Step 1: 既存 3 シナリオへ「F 長押しで確定」を足す**

各シナリオのローカル関数群（`#region Internal`）へ次を足し、撤去を期待する `p.Until(...)` の直前で呼ぶ:

```csharp
    // 新方式: 選択後に F を 2 秒長押しして確定する
    // New flow: hold F for 2 seconds after selecting to confirm
    async UniTask HoldDeleteConfirm()
    {
        p.Note("F長押しで削除を確定");
        SemanticInput.KeyDown(Key.F);
        await p.WaitSeconds(Client.Game.InGame.UI.UIState.State.DragDelete.Confirm.DeleteHoldConfirm.HoldSeconds + 0.3f);
        SemanticInput.KeyUp(Key.F);
        await p.WaitSeconds(0.5f);
    }
```

- `connect/delete-tool-cut-and-undo-connection-lines.cs`: `ClickLine(...)` の末尾（`MouseButtonUp(0)` の後）に `await HoldDeleteConfirm();` を足す。Step 3 のドラッグ（`SemanticInput.MouseButtonUp(0);`）の直後にも `await HoldDeleteConfirm();`。
- `building/build-undo-ctrl-z.cs`: 削除ツールでクリック／ドラッグした直後の `MouseButtonUp(0)` の後へ `await HoldDeleteConfirm(p);`（ローカル関数が `p` を引数で受ける形式なので `HoldDeleteConfirm(PlaytestDriver p)` に合わせる）。
- `train/delete-tool-pier-removal-undo-rail.cs`: 同様に `MouseButtonUp(0)` の後へ `await HoldDeleteConfirm();`。加えて、最後のレール復元アサートの後に次の 1 ステップを足す（レールを跨ぐ箱）:

```csharp
    // Step N: レール edge を跨ぐ箱で選び、F長押しで edge が消える
    // Step N: box-select across a rail edge and confirm with F; the edge disappears
    p.Note("Step N: Shift+クリック2点でレールを跨ぐ箱 → 件数1以上 → F長押し");
    var railEdgeCountBefore = CountClientRailEdges();
    await AimAtGround(railMidCellSouthWest);          // 既存シナリオのレール中点の手前セル（地面上）を照準するヘルパー。無ければ p.AimAt(セル中心 - 0.02f の高さ) で作る
    SemanticInput.KeyDown(Key.LeftShift); await UniTask.DelayFrame(3);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(5); SemanticInput.MouseButtonUp(0); await UniTask.DelayFrame(3);
    SemanticInput.KeyUp(Key.LeftShift);
    await AimAtGround(railMidCellNorthEast);
    await p.Until(() => TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange) >= 1, 5f, "StepN: 範囲内の対象が1件以上");
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(5); SemanticInput.MouseButtonUp(0);
    await HoldDeleteConfirm();
    await p.Until(() => CountClientRailEdges() < railEdgeCountBefore, 15f, "StepN: 箱で選んだレール edge が撤去される");
    int CountClientRailEdges() => Client.Game.InGame.Train.RailGraph.TrainRailObjectManager.Instance.EnumerateRailChains().Count();
```

（`TooltipParam` は下の新規シナリオと同じ実装。`railMidCellSouthWest/NorthEast` は既存シナリオのレール座標から、レール中点を挟む 2 セルを選ぶ）

- [ ] **Step 2: 新規シナリオを書く**

```csharp
// 削除ツールの新方式: なぞり選択は離しても残り、F長押し(2秒)で確定。Shift+クリック2点の箱で範囲選択。ESCで全解除。Ctrl+Zで復元
// New delete-tool flow: stroke selection survives release, F hold (2s) confirms; Shift+click two-point box selects a range; ESC clears; Ctrl+Z restores
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using static Client.Playtest.Operations.Ui.DeleteBoxScenarioInput;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using UnityEngine;
using UnityEngine.InputSystem;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("delete-tool-box-select-hold-confirm", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());
    p.WarpPlayer(new Vector3(2.5f, 33.5f, -2f));

    // 準備: 木のチェストを一列に3つ直置き
    // Setup: place three wooden chests in a row directly
    var cells = new[] { new Vector3Int(2, 32, 4), new Vector3Int(4, 32, 4), new Vector3Int(6, 32, 4) };
    foreach (var cell in cells) PlaytestBlockOps.PlaceBlockDirect("木のチェスト", cell, BlockDirection.North);
    foreach (var cell in cells) await p.WaitBlockGameObject(cell);
    await p.Screenshot("01-prepared");

    await p.PressKey(Key.G);
    await p.WaitUiState(UIStateEnum.DeleteBar, 10f);

    // Step 1: なぞって離しても消えない
    // Step 1: a stroke that is released does not delete
    p.Note("Step 1: チェスト1をなぞって離す→赤いまま残り、消えない");
    await AimAtCell(p, cells[0]);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(1.0f);
    p.Assert(p.GetBlock(cells[0]) != null, "Step1: 離しただけでは撤去されない");
    p.Assert(TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 1, $"Step1: ツールチップに選択件数1が出る 実際:{TooltipText()}");
    await p.Screenshot("02-stroke-kept");

    // 箱待ちだけを畳み、選択を残す。Fの対象はホバーでなく選択になる
    // Fold only the pending box, keeping the selection; F must target selection rather than hover
    await CheckEmptyBoxAndHeight(p, new Vector3Int(8, 32, 6), 1);
    await AimAtCell(p, cells[1]);

    // Step 2: F長押しで確定
    // Step 2: F hold confirms
    p.Note("Step 2: F長押し2秒で選択を削除");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds * 0.5f);
    await p.Screenshot("03-holding");
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds * 0.5f + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[0]) == null, 15f, "Step2: F長押し満了でチェスト1が撤去される");
    p.Assert(p.GetBlock(cells[1]) != null, "Step2: 選択があるとき別のホバー対象を消さない");
    await p.Screenshot("04-deleted");

    // Step 3: Shift+クリック2点の箱でチェスト2・3を選ぶ（終点確定だけでは消えない）
    // Step 3: box-select chests 2 and 3 with Shift+click then click; confirming the end alone deletes nothing
    p.Note("Step 3: Shift+クリックで始点、クリックで終点→範囲内2件");
    await BoxSelect(cells[1] + new Vector3Int(-1, 0, -1), cells[2] + new Vector3Int(1, 0, 1), 2, "Step3");
    p.Assert(p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, "Step3: 終点確定だけでは撤去されない");
    await p.Screenshot("06-box-selected");

    // Step 4: ESCで全解除→再び箱→F長押し
    // Step 4: ESC clears, box again, then F hold
    p.Note("Step 4: ESCで全解除（赤が戻る）→もう一度箱で選び直しF長押し");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.DeleteBar, "Step4: 選択があるときのESCは破壊モードに留まる");
    p.Assert(TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 0, $"Step4: 全解除で件数行が消える 実際:{TooltipText()}");
    await BoxSelect(cells[1] + new Vector3Int(-1, 0, -1), cells[2] + new Vector3Int(1, 0, 1), 2, "Step4");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[1]) == null && p.GetBlock(cells[2]) == null, 15f, "Step4: 箱の2件がまとめて撤去される");
    await p.Screenshot("07-box-deleted");

    // Step 5: Ctrl+Zで2件が戻る
    // Step 5: Ctrl+Z restores both
    p.Note("Step 5: Ctrl+Zで箱の2件が1バッチで戻る");
    foreach (var (key, down) in new[] { (Key.LeftCtrl, true), (Key.Z, true), (Key.Z, false), (Key.LeftCtrl, false) })
    {
        if (down) SemanticInput.KeyDown(key);
        else SemanticInput.KeyUp(key);
        await UniTask.DelayFrame(3);
    }
    await p.Until(() => p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, 15f, "Step5: Ctrl+Zで2件とも復元される");
    await p.Screenshot("08-undone");

    // 押下中ESC後は離すまで新しいホバーを消さず、離した次の押下は単体撤去する
    // ESC during a hold blocks the new hover until release; the next hold deletes the hovered object
    await AimAtCell(p, cells[1]);
    await Click();
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(0.5f);
    await p.PressKey(Key.Escape);
    await AimAtCell(p, cells[2]);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds + 0.3f);
    p.Assert(p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, "ESC後のF押し続けは選択もホバーも撤去しない");
    p.Assert(TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount) == 0, "選択解除ESCで選択が空になる");
    SemanticInput.KeyUp(Key.F);
    await UniTask.DelayFrame(3);
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[2]) == null, 15f, "空選択ならFでホバー1件を撤去する");
    p.Assert(p.GetBlock(cells[1]) != null, "ホバー以外のブロックを残す");
    await p.PressKey(Key.Escape);
    await p.WaitUiState(UIStateEnum.GameScreen, 10f);
    await p.Screenshot("09-hover-and-exit");

    #region Internal

    // Shift+クリックで始点、クリックで終点。終点待ち中の件数が expectedCount になることを確かめる
    // Shift+click the start, click the end; assert the pending count equals expectedCount
    async UniTask BoxSelect(Vector3Int startCell, Vector3Int endCell, int expectedCount, string label)
    {
        await AimAtGround(p, startCell);
        SemanticInput.KeyDown(Key.LeftShift);
        await UniTask.DelayFrame(3);
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        SemanticInput.MouseButtonUp(0);
        await UniTask.DelayFrame(3);
        SemanticInput.KeyUp(Key.LeftShift);
        await p.Until(() => GameObject.Find("DeleteBoxStartMarker") != null && GameObject.Find("DeleteBoxStartMarker").activeSelf, 5f, $"{label}: 始点マーカーが出る");
        await AimAtGround(p, endCell);
        await p.Until(() => TooltipParam(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange) == expectedCount, 5f, $"{label}: 範囲内の対象{expectedCount}件 実際:{TooltipText()}");
        await p.Screenshot($"05-{label}-box-end-hover");
        SemanticInput.MouseButtonDown(0);
        await UniTask.DelayFrame(5);
        SemanticInput.MouseButtonUp(0);
        await p.WaitSeconds(0.5f);
    }

    #endregion
});
```

`MouseCursorTooltipState` の DI 解決は `BlueprintCopyPasteScenarioChecks` が受け取っている形（`PlaytestDriver` 経由）があればそれに合わせる。`TooltipPresentation.Lines` の要素型・`TextParams` の型（`IReadOnlyList<string>` か配列か）は `TooltipTopic.ToDto` の使い方に合わせる。

- [ ] **Step 3: 実行して 4 条件（動画・result.json 全 PASS・スクショ・実プレイ視点）を確かめる**

```bash
SKILL=.claude/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action stop
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/building/delete-tool-box-select-hold-confirm.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/building/build-undo-ctrl-z.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs"
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/train/delete-tool-pier-removal-undo-rail.cs"
```

Expected: 4 本とも `result.json` の Asserts 全 PASS、動画が 0 byte でない。失敗したら `references/troubleshooting.md` に従って診断し、シナリオ側の照準点・待機を直す（本体コードの不具合なら該当タスクへ戻って直す）。

- [ ] **Step 4: コミットする**

```bash
git add .claude/skills/unity-playmode-recorded-playtest/scenarios
git commit -m "test: 削除ツールの選択積み上げ・F長押し・箱選択の録画シナリオを足し既存3本を新方式へ更新"
```

---

### Task 8: 全ブランチレビュー（必須）

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー依頼文の「本PR外のリファクタ提案」節に Phase 2.6 第3バケツを載せる: (1) 既存 `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintCopyTargetRule.cs` の `CreateBox`／`IsCopiedByBox` と、クライアント側で同じ占有セル交差判定を使う箇所の共通化余地（本 plan は既存を直接使う形に改めたので新設側の複製は無い）、(2) 既存 `Blueprint/Copy/BlueprintCopyClickInput.cs`（UI 外の押下→解放を 1 クリックに畳む）と新設 `DeleteBoxInput.ConsumeClick` の同形（Shift の所有判定と UI 上解放の理由付き破棄が増えているだけ）。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 7 の録画シナリオ 4 本を反映後のバイナリで再実施してから完了とする。

### Task 9: セッション終了可能状態にする（必須）

- [ ] pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 作成後に `moores-wt rm delete-tool-hold-confirm-box-select` で worktree を畳む。bd `moorestech-wmhf8` を close する。

---

## 判断記録（ADR）

設計 ADR: `docs/adr/0079-delete-tool-accumulates-selection-and-confirms-with-f-hold-and-box-select.md`（ユーザー裁定は `.decisions/2026-10-16-削除ツール*.md` 12 件）。前提: ADR 0076、ADR 0046、ADR 0032。

planning 中に生じた判断（出所の 3 種: ユーザー裁定／シミュレーター予測→ユーザー承認／agent前提（拒否権つき））:

- 箱の高さオフセットは DI の `PlacementHeightOffset` シングルトンを BP コピーと同じく共用し、箱を畳む・削除モード離脱で `ResetToGround()`。削除モードでも HUD に高さが出る。棄却: 削除ツール専用インスタンス（HUD に出ない）。出所: ユーザー裁定 2026-10-16 選択「共用し、削除モード離脱・箱クリアで地表へ戻す」（review 判事の要裁定・予測 B 的中）
- 長押しの対象は `object/null` でなく判別子付きの `DeleteHoldSubject { None | Selection(revision) | Hovered(key) }`。出所: シミュレーター予測→ユーザー承認 2026-10-16（前提宣言に異議なし。前例 `.decisions/2026-10-05-削除ツールの照準条件は判別unionで表す.md`）
- 箱の件数表示・0 件拒否・追加は `DeleteBoxTargetResolver.Resolve` が撤去可否を判定して返す 1 本の列を使い、撤去不可は Resolver が件数と理由をログ。`AddBoxTargets` は足すだけ。出所: シミュレーター予測→ユーザー承認 2026-10-16（前提宣言に異議なし。Phase 2.6 検査6）
- 箱の局面は `enum DeleteBoxPhase`＋`StartCell`＋throw ガードのまま（`BlueprintCopySelection` と同形の前例）。棄却: 局面ごとのペイロード型。出所: ユーザー裁定 2026-10-16 選択「このままでよい」（Phase 2.6 5-A）
- 箱入力の 1 フレーム結果 `DeleteBoxStep` 構造体は作らず、`DeleteBoxInput.Update(DeleteSelection, lines, isPointerOverUi)` が終点確定のフレームに `AddBoxTargets` を直接呼ぶ。棄却: bool＋ペイロード構造体／判別子＋ペイロード型。出所: ユーザー委任 2026-10-16 原文「良く分からないけど進めで承認」→ 推奨（review 判事 Warning「引数を返す複雑性」）を採用した agent 判断
- Phase 2.6 の弱い発火 5 件は全部拾う: `TryAddTarget` の失敗理由を `DeleteAddOutcome`（Kind＋DenyReason）で区別、`ConsumeClick` の UI 上解放を `DeleteBoxClick.ReleasedOverUi` として理由付きで捨てる、赤プレビューの書き手を `DeleteSelection`（`SetHoverPreview`）へ一本化し `DeleteHoverPreview` クラスは作らない、破棄済み判定を `IsDestroyed` に集約、左押下の所有判定を `DeleteBoxInput.ClaimsLeftPress()` に寄せてサービスは聞くだけ、`EnumerateRailChains` の `continue` に `silent-ok`。棄却: 3 件だけ／silent-ok だけ。出所: ユーザー裁定 2026-10-16 選択「全部拾う」
- review 判事（user-simulator review モード）の確信あり指摘 5 件を適用: 車両の列挙は `EntityObjectDatastore`（ベルト上アイテムのみ）でなく `TrainCarObjectDatastore.EnumerateTrainCars()`、`RemoveOperationHistoryTest.cs` を改名の波及先に追加、`CellRangeBoxTest` は `Common/RangeBox/` へ（`Common/` 直下 11 ファイル）、ブロックの箱判定は `BlueprintCopyTargetRule.CreateBox`＋`IsOverlap` を直接使い `DeleteBoxRule` に複製しない、`TryCancel` の先頭で `_hold.Reset()`（箱を畳む段でも進捗 0）。Warning から: 左ボタン解放で `EndStroke()` によりカテゴリー固定を最前面へ戻す、橋脚撤去シナリオにレールを跨ぐ箱の 1 ステップを足す、csv の列数表現を直す。出所: シミュレーター予測（review）→ agent 適用（事実は実ファイル行番号で裏取り済み）
- `TrainRailObjectManager.cs` へ `EnumerateRailChains()` を足す（`FindObjectsByType` で設置プレビューのチェーンまで拾うのを避ける）。出所: agent前提
- レール曲線のサンプルは `BezierRailChain.cs`（197 行）に足さず、子 `BezierRailMesh` の制御点を `BezierRailMeshData.EvaluatePosition` で評価する `DeleteBoxRailSampler` を新設する。出所: agent前提（200 行制限）
- 選択モデルは `DragDeleteSelection` → `DeleteSelection` へ改名（持続する選択集合になり「ドラッグ」限定でなくなるため）。ディレクトリ `DragDelete/` と名前空間は据え置く。出所: agent前提
- Q/E は箱の始点・終点どちらのセル高さにも効く（BP コピーと同じ）。ADR 0079 の文面もこれに揃えた。出所: agent前提
- 箱の始点候補マーカーは Shift を押している間だけ出す（箱モードに入っていることの合図）。削除箱の範囲色は `MaterialConst.NotPlaceableColor`、BP コピーは従来どおり `PlaceableColor`。出所: agent前提
- 長押し満了後は F を一度離すまで次の長押しを始めない（押し続けた指がホバー対象を 2 秒後に消す連鎖を防ぐ）。長押し中にストロークで対象を足すと `Revision` が変わり進捗が 0 に戻る。出所: agent前提／ADR 0079
- 箱の対象解決（`DeleteBoxTargetResolver`）と入力（`DeleteBoxInput`）は EditMode ユニットテストで直接は検証しない（`BlockGameObjectDataStore`・レール・カメラのレイキャストが要る）。純関数 `DeleteBoxRule`・モデル `DeleteBoxSelection`・`DeleteHoldConfirm`・`DeleteSelection` をユニットテストで固め、配線は Task 7 の unityプレイ録画テスト（レールを跨ぐ箱を含む）で通す。EditModeInPlayingTest は録画テストが上位互換のため作らない。出所: agent前提
- `ProgressBarState` の `Show/Hide/SetProgress` は internal だが `DeleteHoldConfirm` は同アセンブリ（Client.Game）なので採掘と同じく直接呼ぶ。出所: agent前提（MiningProgressState 前例）
- `TrainCarObjectDatastore.cs`・`TrainRailObjectManager.cs` への列挙口追加は読み取り専用で、変更系は露出しない（Datastore レンズ対象）。出所: agent前提

### Task 1〜6 レビュー反映

- Resolver は動く車両の範囲出入りを表示へ反映するため毎フレーム再解決する。同じ箱操作の診断は `HashSet<string>` で初回だけ出す（前例 `BlockGameObjectColliderSetup`）。範囲のキャッシュによる古い件数表示は採用しない。mesh 欠落診断も Resolver に集める。
- `DeleteBoxInput.Update` はサービス側で一度だけ評価した `isPointerOverUi` を受け取る。`ClearDiagnostics` は箱を畳む時に呼ぶ内部API。`DeleteBoxRailSampler` は Resolver が確認済みの `BezierRailMesh` を受ける。
- `DeleteHoldConfirm.Cancel(bool isKeyHeld)` は押下中のキャンセルに離すまでの再確定抑止を付ける。`Reset` は進捗だけを戻す。非ストローク中のホバーは左ボタン押下中も照準に追従する。
- Task 7 の新規録画では、箱待ちESCで既存選択を残す、次のESCで選択解除、空のESCでモード終了、0件拒否で終点待ち維持、Q/Eで共用高さとマーカー位置を往復、箱キャンセルで高さ0、選択がホバーより優先、押下中ESC後の再確定抑止、空選択のホバー確定を実測する。
- ホバー暗黙確定の最終再検査で拒否される防御分岐は、同期フレーム内の可否反転を実入力から安定して発火できないため、録画で通ったとは扱わない。

## レビュー反映の委任既定: D1/D2

ユーザーの推奨案実行委任に基づくagent既定であり、新しいユーザー裁定ではない。R13の個別送信・サーバー無変更を維持する。

- Interfaces: IDeleteTarget.OnDeleteCompleted は TargetKey/Kind/DenyReason を通知する。Kind は Pending/Succeeded/Rejected/Unconfirmed。block/rail/wire/chain は既存個別応答を受信する。車両は既存同期のtick完了後に全編成のcar ID不在を確認し、ID無し拒否通知に対応するため送信を直列化する。送信済み/表示体破棄は成功根拠にしない。
- Architecture: DeleteCommitBatch は送信前に各対象の復元情報を採取し、BuildOperationHistory.Reserve/Completeで成功分だけのUndoを作る。車両の未確認はUndo対象群の完了を待たせない。DeleteResultFeedbackはホバーと独立に対象別結果を保持し、モード離脱時も既存通知へ結果を届ける。
- 選択規則はDeleteSelection.UpdateStroke/UpdateHoverPreview/TryCommit/TryCancelSelectionへ集約する。OnSelectionChangedを購読してDeleteHoldConfirm.SetSubjectへプッシュし、Advanceはキー状態と時間だけを進める。
- 情報不足: 車両要求には対象ID付き応答がない。既存通知で確認できない場合はUnconfirmedとして表示し、遅延通知の誤帰属を避け後続車両要求を保留する。後から同期結果が判明すれば表示を更新し保留を再開する。他プレイヤーの同時削除とは因果を区別できず、確認できるのは対象不在という達成状態である。

## レビュー反映の委任既定: D3/D6

Architecture: block/car/railのownerは初期化またはRebuildで代表削除対象を保持する。Resolverは既存ownerの参照を読み、構成を毎フレーム再検索しない。railのRebuild/Clearで古い子参照を破棄し新子へ交換する。曲線はSetControlPoints時にBezierCellBoxIntersectionの係数を更新する。対象列・車両位置・撤去可否は毎フレーム更新する。

Interfaces: BezierCellBoxIntersection.Intersects(min,max)は連続3次Bezierと半開セル箱を評価する。各軸の境界根と微分根により単調区間を分け、境界点と区間内部を検査する。旧DeleteBoxRailSampler/SampleCount=64は廃止し、計画内の64点方式の記述はこの変更で置き換える。ユーザー承認済みの「途中が箱に触れれば対象」を優先したagent既定であり、新しいユーザー裁定ではない。

## レビュー反映の委任既定: D4/W02

DeleteObjectState.GetNextUpdateがUIポインター判定を1回取得し、右短押し、スポイト、削除サービスへ同じboolを渡す。helper内の再問い合わせを廃止し他state callerも明示値を渡す。

DeleteBoxSelectionは最後の有効終点を保持する（始点選択時は始点を初期終点とする）。カーソルの面が解決できない間も、その箱を表示して毎フレーム対象を解決し件数を表示する。既存の地面未検出理由を併記してクリック確定を拒否する。有効なカーソルが戻れば終点を更新する。件数そのものはキャッシュしない。
