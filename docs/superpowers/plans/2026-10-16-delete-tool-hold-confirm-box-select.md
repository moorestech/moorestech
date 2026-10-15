# 削除ツール: 選択積み上げ・F長押し確定・箱選択 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 削除ツールを「なぞり・箱で対象を選択へ積み上げ、F を 2 秒長押しして一括撤去する」方式へ変え、Shift+左クリック 2 点の直方体（箱）による範囲選択を足す。

**Architecture:** クライアントのみの変更。`DeleteObjectState` が毎フレーム `DeleteObjectService.Update(deltaTime)` を駆動し、サービスが入力（左ドラッグのなぞり／Shift+左クリックの箱／F 長押し／Q・E）を検知して選択モデル `DeleteSelection` へ「足す・全解除・確定」を指示する。箱は `DeleteBoxInput`（入力・セル解決・可視化）→ `DeleteBoxTargetResolver`（AABB に入る `IDeleteTarget` の列を返す 1 本の問い合わせ）→ `DeleteSelection.AddBoxTargets` の一方向。長押しは `DeleteHoldConfirm` が採掘と同じ `ProgressBarState` へ進捗を流す。サーバー・プロトコルは変更しない。

**Tech Stack:** Unity (C#)、UniRx、Unity InputSystem（`InputManager` / `HybridInput`）、NUnit EditMode テスト（`Client.Tests`）、unity-playmode-recorded-playtest（プレイテスト DSL）。

## Requirements

設計 ADR: `docs/adr/0079-delete-tool-accumulates-selection-and-confirms-with-f-hold-and-box-select.md`。裁定: `.decisions/2026-10-16-削除ツール*.md`（10 件）。

- R1 なぞり選択（左ドラッグ）は残す。左ボタンを離しても選択は消えず、何度なぞっても同じ選択集合へ追加される。受け入れ: なぞって離した後も対象は赤プレビューのまま残り、撤去要求は送られない。
- R2 1 回のなぞり（ストローク）に入るのは同じ破壊カテゴリーだけ（現行どおり）。カテゴリー固定はストロークごとにリセットされ、箱の影響を受けない。受け入れ: 既存 `DragDeleteSelectionCategoryTest` 相当が通る。
- R3 削除の確定は F キー長押し（`InputManager.Playable.Interact`）。件数に関係なく一定 2 秒（定数）。進捗は採掘と同じ画面固定の進捗バー（`ProgressBarState`）へ 0→1 で出す。受け入れ: 2 秒未満で離すと進捗が 0 に戻り選択は残る。満了で選択を全部撤去し 1 つの Undo バッチを積む。
- R4 F 長押しは「F を離す」「選択集合が変わる」「（空選択時）ホバー対象が変わる」「ESC」のいずれでも進捗 0 に戻る。満了後は F を一度離すまで次の長押しを始めない。受け入れ: `DeleteHoldConfirmTest`。
- R5 選択が空で F を長押しすると、ホバー中の 1 件（撤去可能なもの）を暗黙の選択として撤去する。選択が 1 件以上あればホバー対象は含めない。ホバーも選択も無ければ進捗バーを出さない。
- R6 箱選択: Shift を押しながら左クリック（UI 外で押下→解放）で始点セル、次の左クリックで終点セル。セルは 1x1 設置と同じ `PlacementUnitCellResolver.TryGetCursorCell`。Q/E で高さオフセット（削除ツール専用の `PlacementHeightOffset` インスタンス。始点・終点どちらにも効く）。
- R7 箱は始点・終点セルを対角とする AABB（セル [min, max] を含む直方体）。破壊カテゴリーを問わず、占有セルが 1 つでも交差するブロック・曲線上の点が 1 つでも箱内のレール edge・位置が箱内の列車車両を全て選択へ足す。接続線は箱では選ばない。遮蔽は問わない（レイキャスト不使用）。
- R8 箱の終点待ち中はカーソルツールチップへ「範囲内の対象: N」を常時出し、0 件の終点クリックは追加せずツールチップ（「範囲に対象がありません」）とログへ理由を残して終点待ちのまま。終点待ち中は左クリックを終点確定に使い、なぞりは始まらない。
- R9 箱で足した対象は選択時に固定（対象の集合）。後から箱内へ入った対象は含めず、選んだ車両が動いても消す。選択後に破棄された対象は確定時に読み飛ばす（既存の `destroyed before commit`）。
- R10 選択の個別除外は作らない。ESC／パネル外右短押しは「終点待ちの箱を畳む → 選択の全解除 → 破壊モード終了」の順で 1 段ずつ効く。モード離脱（G/B/Tab/スポイト）で選択・箱・進捗を全て片付ける。
- R11 選択がある間はカーソルツールチップに「選択中: N件」を常時出す。拒否理由（異なるカテゴリー・撤去不可）は同じツールチップの別行として出す。
- R12 箱で非撤去可の対象（駅内部レール等）は足さず、件数と理由を `Debug.Log` へ残す（無音の縮退禁止）。
- R13 一部が撤去できないときは消せる対象だけ消し、拒否理由を対象ごとに現行どおり表示（サーバー変更なし・個別送信のまま）。
- R14 ホバーの赤プレビューと選択済みの赤プレビューは同じ表示。ホバーが選択済み対象から外れてもその対象の赤は戻さない。
- R15 キーヒント（破壊モード）: 左ドラッグ まとめて選択／Shift+左クリック 範囲選択／Q 範囲の高さを下げる／E 範囲の高さを上げる／F（長押し） 選択を削除／G 破壊モード終了／B ビルドメニュー／Tab インベントリ／中クリック 設置物をスポイト／Ctrl+Z 元に戻す／V 視点切替（11 件）。ADR 0032 の表と `UIStateKeyHintCatalogTest` を同時更新。
- R16 BP コピーの範囲ビジュアライザとセル AABB 計算を共通置き場へ移し、削除ツールと共用する（BP コピーの GameObject 名 `BlueprintCopyStartMarker` 等と挙動は不変）。
- R17 既存の録画シナリオ（削除をクリック／ドラッグで行う 3 本）を新方式（選択後 F 長押し）へ更新し、新規の録画シナリオで「なぞり→離しても残る→F 長押しで消える／箱選択→件数→F 長押し／ESC 全解除／Ctrl+Z」を通す。
- やらないこと: 一括撤去プロトコルの新設、選択の個別除外、選択上限、サーバー側の変更、箱による接続線の直接選択、列車車両の Undo。

## Global Constraints

- AGENTS.md: 1 ファイル 200 行以下、1 ディレクトリ 10 ファイルまで、partial 禁止、`Func<>` 禁止、try-catch 原則禁止、デフォルト引数禁止、イベントは UniRx、単純 getter/setter 禁止（`{ get; private set; }` は可）、`[SerializeField]` は小文字キャメル、日英 2 行コメント、`#region Internal` はメソッド内ローカル関数のみ、無音の縮退禁止（スキップ・拒否は理由をログへ）、`.meta` は手で作らない（`git mv` でファイルと `.meta` を一緒に移す）。
- 時間: クライアントの長押し進捗は採掘と同じく `Time.deltaTime` 累積（`DeleteObjectState` が `Time.deltaTime` を渡す）。サーバーの時間 API は触らない。
- 入力: F は `InputManager.Playable.Interact.GetKey`、Shift は `HybridInput.GetKey(KeyCode.LeftShift)`（前例 `CommonBlockPlaceSystem.cs:128`）、Q/E は `PlacementHeightKeyInput.Apply`、左クリックは `InputManager.Playable.ScreenLeftClick`、UI 上判定は `UiPointerHitTest.IsPointerOverAnyUi()`。
- ローカライズ: キーは `Localization/localization.csv` に 6 言語列で追加。C# 側は `uloop compile --project-path ./moorestech_client --force-recompile` の後に通常 `uloop compile`（`docs/development/uloop-troubleshooting.md`）。Web UI 側は `cd moorestech_web/webui && npm run gen:i18n` で `src/shared/i18n/generated/localizationKeys.ts` を再生成してコミット。
- 定数: 長押し満了 `DeleteHoldConfirm.HoldSeconds = 2f`。レール曲線のサンプル数は `BezierRailMesh` の既定 64 点。
- 命名: 選択モデルは `DeleteSelection`（旧 `DragDeleteSelection`）。ディレクトリ `UI/UIState/State/DragDelete/` と名前空間 `Client.Game.InGame.UI.UIState.State.DragDelete` は据え置く（7 ファイル＋テスト多数の改名を避ける）。新規ファイルは `DragDelete/Box/`・`DragDelete/Confirm/` へ置く（10 ファイル制限）。
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "<FullName>"`。ドメインリロードエラー時は 45 秒待って再試行。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正1件 / 配置表・データフロー地図・死活表を plan 本文に記載。Self-Review でホバー解放時の赤プレビュー残留（非撤去可対象）を見つけ Task 6 のコードを修正
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| 区分 | パス | 責務 |
|---|---|---|
| Create | `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox/CellRangeBox.cs` | 2 セルから AABB（min/max）と世界座標 Bounds を計算する純関数 |
| Move | `.../PlaceSystem/Blueprint/Copy/BlueprintCopyRangeVisualizer.cs` → `.../PlaceSystem/Common/RangeBox/CellRangeBoxVisualizer.cs` | 始点・終点マーカーと範囲箱の表示（名前接頭辞と範囲色を外から渡す） |
| Modify | `.../PlaceSystem/Blueprint/Copy/BlueprintCopySystem.cs` | 共通ビジュアライザへ差し替え |
| Modify | `.../PlaceSystem/Blueprint/Copy/BlueprintCopySelection.cs` | `CalcBox` を `CellRangeBox.Calc` へ委譲 |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxRule.cs` | 箱に入る判定の純関数（ブロック占有セル交差・点・曲線サンプル） |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxRailSampler.cs` | `BezierRailChain` の曲線を 64 点サンプルして箱と照合 |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxTargetResolver.cs` | AABB に入る `IDeleteTarget` の列を返す 1 本の問い合わせ（ブロック・レール・車両） |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxSelection.cs` | 箱の局面（Idle/SelectingEnd）と始点セルのモデル |
| Create | `.../UI/UIState/State/DragDelete/Box/DeleteBoxInput.cs` | Shift+クリック／クリック／Q・E の検知、セル解決、可視化、完了時の対象列の受け渡し |
| Create | `.../UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs` | F 長押しの進捗（2 秒）と進捗バー駆動 |
| Rename | `.../UI/UIState/State/DragDelete/DragDeleteSelection.cs` → `DeleteSelection.cs` | 選択集合（持続・箱追加・Contains・Revision） |
| Create | `.../UI/UIState/State/DragDelete/DeleteHoverPreview.cs` | 単体ホバーの赤プレビュー所有（選択済みは戻さない） |
| Modify | `.../UI/UIState/State/DragDelete/DeleteObjectService.cs` | 毎フレームの司令塔（箱→なぞり→ホバー→長押し→ツールチップ） |
| Modify | `.../UI/UIState/State/DeleteObjectState.cs` | 依存追加（ProgressBarState・Camera・DataStore）、deltaTime 受け渡し、ヒント 11 件 |
| Modify | `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs` | `EnumerateRailChains()` 追加 |
| Modify | `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs` | `EnumerateEntityObjects()` 追加 |
| Modify | `Localization/localization.csv`、`moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` | キー追加・再生成 |
| Modify | `docs/adr/0032-key-hint-hud-owned-by-ui-state.md` | 破壊モード行を 11 件へ |
| Test | `Client.Tests/PlaceSystem/Common/CellRangeBoxTest.cs`、`Client.Tests/UIState/DragDelete/DeleteBoxRuleTest.cs`、`Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs`、`Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs`、`Client.Tests/UIState/DeleteSelectionTest.cs`（旧 `DragDeleteSelectionTest.cs`）、既存 `DragDeleteDenyReasonTest.cs`／`DragDeleteSelectionCategoryTest.cs`／`DragDelete/DestroyedSelectionTest.cs`／`UIStateKeyHintCatalogTest.cs`／`UIStateFocusRestorationTest.cs`／`UIStateCameraInteractionTest.cs`／`RightShortPressTransitionTest.cs` | 更新 |
| Scenario | `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool-box-select-hold-confirm.cs`（新規）、`scenarios/building/build-undo-ctrl-z.cs`／`scenarios/train/delete-tool-pier-removal-undo-rail.cs`／`scenarios/connect/delete-tool-cut-and-undo-connection-lines.cs`（更新） | unityプレイ録画テスト |

### データフロー地図（Phase 1.5）

```
（DeleteObjectState.GetNextUpdate 毎フレーム）
  → DeleteObjectService.Update(deltaTime)
      → DeleteBoxInput.Update(lines)            … Shift+クリック/クリック/Q・E → DeleteBoxSelection（局面）→ 終点確定時 DeleteBoxTargetResolver.Resolve(min,max)
      → DeleteSelection.AddBoxTargets / TryAddTarget / CancelSelection / CommitDelete   ［共有選択集合＝唯一の書き込み先］
      → DeleteHoverPreview.Update(hovered)      … 読み手（赤プレビュー表示のみ）
      → DeleteHoldConfirm.Advance(F, subject, dt) → 満了で DeleteSelection.CommitDelete
      → IMouseCursorTooltip.Show(lines)         … 読み手（件数・拒否理由）
      → ProgressBarState.SetProgress            … 読み手（進捗 HUD）
```

新規コンポーネントは全て「DeleteSelection への書き手」か「読み手」であり、既存フローへの bool 戻り値の逆流や第 2 の書き込み経路は足さない。既存の `DeleteTargetRaycaster.AimAt(filter)`（照準）と `RemoveOperationRecord`（Undo 記録）はそのまま使う。

### 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| 入力→選択反映サービスをステートから毎フレーム駆動 | `DeleteObjectService.Update(deltaTime)` | `PlaceSystemStateController.ManualUpdate()`、既存 `DeleteObjectService.Update()` |
| 長押し進捗 | `DeleteHoldConfirm` → `ProgressBarState`（同アセンブリの internal Show/Hide/SetProgress） | `MiningProgressState`（`Client.Game/InGame/Mining/`） |
| 2 クリックのセル AABB・Q/E | `DeleteBoxInput` が `PlacementUnitCellResolver` と `PlacementHeightKeyInput` を使う | `BlueprintCopySystem`（`.decisions/2026-10-08-BPコピー*.md`） |
| 範囲の可視化 | 共通 `CellRangeBoxVisualizer` | 旧 `BlueprintCopyRangeVisualizer` |
| 箱の対象解決を 1 本の問い合わせに閉じる | `DeleteBoxTargetResolver.Resolve(min,max)` が種類別判定を内包し、呼び出し側は列を足すだけ | 原則「判断を内包する新設サービスは指示を返す」（`.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`） |
| ツールチップ行の積み上げ | `List<TooltipLine>` を 1 フレームで組み `IMouseCursorTooltip.Show(owner, lines)` | `TapInteractionDriver.ShowHints`、`BlueprintCopyFeedbackLines` |
| 列車車両・レールの列挙 | `EntityObjectDatastore.EnumerateEntityObjects()`、`TrainRailObjectManager.EnumerateRailChains()` | `BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary`（BP コピーの計数が使う） |

### 死活表（Phase 2.5）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 左ドラッグでなぞり選択 | 生きる（離しても残る＝裁定どおりの変更） | R1 |
| 単体のホバー→即削除 | 変わる: ホバー→F 長押し 2 秒（裁定 `.decisions/2026-10-16-削除ツールは選択が空ならF長押しでホバー中の1件を消す.md`） | R5 |
| ESC／右短押しの二段階解除（ADR 0046） | 生きる（三段階: 箱→選択→離脱） | R10 |
| G/B/Tab/中クリックの遷移 | 生きる | `DeleteObjectState.HandleTransition` 不変 |
| Ctrl+Z（撤去の取り消し） | 生きる | `BuildUndoService.ManualUpdate` 不変・`CommitDelete` が記録を積む |
| 電線ツール等の Shift+R（垂直回転） | 影響なし（破壊モードでは走らない） | 別ステート |
| 設置モードの Q/E・HUD 高さ表示 | 影響なし（削除ツールは専用 `PlacementHeightOffset` を持つ） | R6 |
| BP コピーの始点・終点マーカー／範囲箱の名前・色 | 生きる（`"BlueprintCopy"` 接頭辞・`PlaceableColor` を渡す） | R16 |
| 録画シナリオ 3 本の削除手順 | 更新が要る（F 長押しを足す） | R17 |

---

### Task 1: セル範囲の共通部品（CellRangeBox・CellRangeBoxVisualizer）と BP コピーの差し替え

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox/CellRangeBox.cs`
- Move (`git mv` でファイルと `.meta` を一緒に): `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopyRangeVisualizer.cs` → `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox/CellRangeBoxVisualizer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopySystem.cs:38,62`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy/BlueprintCopySelection.cs:52-55`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/CellRangeBoxTest.cs`

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

namespace Client.Tests.PlaceSystem.Common
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

`BlueprintCopySystem.cs`: `using Client.Game.InGame.BlockSystem.PlaceSystem.Common.RangeBox;` と `using Client.Common;` を足し、フィールド型とEnableを次へ。

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
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/RangeBox moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/Copy moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Common/CellRangeBoxTest.cs
git commit -m "refactor: BPコピーの範囲ビジュアライザとセルAABB計算を共通のRangeBoxへ移す"
```

---

### Task 2: 箱に入る判定（DeleteBoxRule・DeleteBoxRailSampler）と列挙口の追加

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxRule.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box/DeleteBoxRailSampler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs:47-55`（`TryGetRailChain` の直後に追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs:16-20`（`_entities` の直後に追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxRuleTest.cs`

**Interfaces:**
- Consumes: `BlockPositionInfo(Vector3Int originalPos, BlockDirection direction, Vector3Int size)`, `BlockPositionInfo.IsOverlap(BlockPositionInfo)`（`Game.Block.Interface`）、`BezierRailMeshData.EvaluatePosition(BezierRailMesh mesh, float t)`（internal static、同アセンブリ `Client.Game`）、`BezierRailChain`
- Produces:
  - `public static class DeleteBoxRule { public static BlockPositionInfo CreateBox(Vector3Int min, Vector3Int max); public static bool IsBlockInBox(BlockPositionInfo blockPosition, BlockPositionInfo box); public static bool IsPointInBox(Vector3 point, Vector3Int min, Vector3Int max); public static bool IsAnyPointInBox(IReadOnlyList<Vector3> points, Vector3Int min, Vector3Int max); }`
  - `public static class DeleteBoxRailSampler { public const int SampleCount = 64; public static bool IntersectsCellBox(BezierRailChain chain, Vector3Int min, Vector3Int max); }`
  - `TrainRailObjectManager.EnumerateRailChains()` : `public IEnumerable<BezierRailChain>`
  - `EntityObjectDatastore.EnumerateEntityObjects()` : `public IEnumerable<IEntityObject>`

- [ ] **Step 1: テストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState.State.DragDelete.Box;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteBoxRuleTest
    {
        [Test]
        public void 占有セルが1つでも交差するブロックは箱に入る()
        {
            var box = DeleteBoxRule.CreateBox(new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0));
            var block = new BlockPositionInfo(new Vector3Int(-2, 0, 0), BlockDirection.North, new Vector3Int(3, 3, 3));
            Assert.IsTrue(DeleteBoxRule.IsBlockInBox(block, box));
        }

        [Test]
        public void 占有セルが箱の外のブロックは入らない()
        {
            var box = DeleteBoxRule.CreateBox(new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 1));
            var block = new BlockPositionInfo(new Vector3Int(2, 0, 0), BlockDirection.North, Vector3Int.one);
            Assert.IsFalse(DeleteBoxRule.IsBlockInBox(block, box));
        }

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
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     箱選択の「箱に入る」判定。セル [min, max] を含む直方体に対する純関数
    ///     Box-selection membership rules: pure functions over the inclusive cell box [min, max]
    /// </summary>
    public static class DeleteBoxRule
    {
        public static BlockPositionInfo CreateBox(Vector3Int min, Vector3Int max)
        {
            return new BlockPositionInfo(min, BlockDirection.North, max - min + Vector3Int.one);
        }

        // 占有セルが1つでも交差すれば丸ごと対象（BPコピーと同じ規則）
        // Any occupied cell intersecting the box includes the whole block (same rule as blueprint copy)
        public static bool IsBlockInBox(BlockPositionInfo blockPosition, BlockPositionInfo box)
        {
            return blockPosition.IsOverlap(box);
        }

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

        public static bool IntersectsCellBox(BezierRailChain chain, Vector3Int min, Vector3Int max)
        {
            // 各メッシュ子は同じ制御点を持つので先頭の1つで曲線を評価する
            // Every mesh child carries the same control points, so evaluate the curve through the first one
            var mesh = chain.GetComponentInChildren<BezierRailMesh>();
            if (mesh == null)
            {
                Debug.LogWarning($"[DeleteBox] rail chain has no BezierRailMesh: {chain.name}");
                return false;
            }

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

（`BezierRailMesh` の名前空間は `Client.Game.InGame.Train.RailGraph` 配下。`BezierRailMeshData.EvaluatePosition` が `internal` で別名前空間なら `using` を実ファイルに合わせる。`BezierRailMeshData` が `internal static class` でない場合は `BezierRailMeshData` の実アクセス修飾子に従い、到達できなければ同クラスへ `internal static Vector3 EvaluatePosition` と同じ形の委譲を足す）

- [ ] **Step 4: 列挙口を足す**

`TrainRailObjectManager.cs` の `TryGetRailChain` 直後:

```csharp
        // 箱選択が全レール edge を走査するための列挙口（破棄済みは飛ばす）
        // Enumeration entry for box selection to scan every rail edge (skips destroyed objects)
        public IEnumerable<BezierRailChain> EnumerateRailChains()
        {
            foreach (var gobj in _railObjs.Values)
            {
                if (gobj == null) continue;
                yield return gobj.GetComponent<BezierRailChain>();
            }
        }
```

`EntityObjectDatastore.cs` の `_creatingEntityIds` 直後（`using System.Collections.Generic;` は既存）:

```csharp
        // 箱選択が車両を走査するための列挙口
        // Enumeration entry for box selection to scan vehicles
        public IEnumerable<IEntityObject> EnumerateEntityObjects()
        {
            foreach (var entry in _entities.Values) yield return entry.objectEntity;
        }
```

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.DragDelete.DeleteBoxRuleTest"`
Expected: ErrorCount 0 / 4 PASS。`TrainRailObjectManager.cs`・`BezierRailChain.cs` が 200 行以下であることを `wc -l` で確認（`BezierRailChain.cs` は触らない）。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box moorestech_client/Assets/Scripts/Client.Game/InGame/Train/RailGraph/TrainRailObjectManager.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxRuleTest.cs
git commit -m "feat: 削除ツールの箱に入る判定（ブロック・レール曲線・点）と列挙口を足す"
```

---

### Task 3: DeleteSelection（持続する選択集合・箱追加・Contains・Revision）

**Files:**
- Rename: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs` → `.../DragDelete/DeleteSelection.cs`（`.meta` も `git mv`）
- Rename: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteSelectionTest.cs` → `.../UIState/DeleteSelectionTest.cs`（`.meta` も）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDeleteDenyReasonTest.cs`、`.../UIState/DragDeleteSelectionCategoryTest.cs`、`.../UIState/DragDelete/DestroyedSelectionTest.cs`（型名・`BeginDrag`→`BeginStroke`・`CanCommit`→`IsStrokeAlive` の置換）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs:2-4`（コメント中の `DragDeleteSelection.CommitDelete` → `DeleteSelection.CommitDelete`）

**Interfaces:**
- Consumes: `IDeleteTarget`、`DeleteAimFilter`、`RemoveOperationRecord.CreateFrom(List<IDeleteTarget>, IRemovalRestoreSender)`、`BuildOperationHistory.Push`
- Produces:
  ```csharp
  public readonly struct DeleteBoxAddResult { public int Added { get; } public int SkippedNonRemovable { get; } }
  public class DeleteSelection
  {
      public DeleteSelection(BuildOperationHistory buildOperationHistory, IRemovalRestoreSender restoreSender);
      public DeleteAimFilter AimFilter { get; private set; }
      public int Revision { get; private set; }          // 追加・全解除・確定のたびに +1
      public int Count { get; }
      public void BeginStroke();                           // なぞり開始: キャンセル解除とカテゴリー固定のリセットだけ（選択は消さない）
      public bool TryAddTarget(IDeleteTarget target, out LocalizationKey? denyReason);   // なぞり追加（カテゴリー固定あり）
      public DeleteBoxAddResult AddBoxTargets(IReadOnlyList<IDeleteTarget> targets);    // 箱追加（カテゴリー不問・非撤去可はログして飛ばす）
      public bool Contains(IDeleteTarget target);
      public void CancelSelection();                       // 全解除（赤プレビューを戻す・ストロークを死に状態へ）
      public void CommitDelete();                          // 一括撤去＋Undo 1バッチ
      public bool IsStrokeAlive();                         // ESC後に離すまで false
      public bool HasSelection();
  }
  ```

- [ ] **Step 1: テストを書く（`DeleteSelectionTest.cs` 全文。旧ファイルの6ケースは `BeginDrag`→`BeginStroke`・`CanCommit`→`IsStrokeAlive` で残す）**

```csharp
using System.Collections.Generic;
using Client.Tests.BuildUndo;
using Client.Game.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
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
            selection.TryAddTarget(target, out _);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.IsTrue(selection.Contains(target));
        }

        [Test]
        public void AddSameTargetTwiceDoesNotDoubleAdd()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target, out _);
            selection.TryAddTarget(target, out _);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.AreEqual(1, selection.Count);
        }

        [Test]
        public void AddNonRemovableTargetIsIgnored()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = false };
            selection.BeginStroke();
            selection.TryAddTarget(target, out _);
            selection.CommitDelete();
            Assert.AreEqual(0, target.SetPreviewCount);
            Assert.AreEqual(0, target.DeleteCount);
        }

        [Test]
        public void CancelSelectionResetsAndKillsStroke()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target, out _);
            selection.CancelSelection();
            Assert.AreEqual(1, target.ResetCount);
            Assert.IsFalse(selection.IsStrokeAlive());
            Assert.IsFalse(selection.HasSelection());
        }

        [Test]
        public void CommitDeleteDeletesTargetThenClears()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(target, out _);
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
            selection.TryAddTarget(new FakeDeleteTarget { Removable = true }, out _);
            selection.CancelSelection();
            selection.BeginStroke();
            Assert.IsTrue(selection.IsStrokeAlive());
        }

        // 新方式: なぞりを離しても（次のストロークを始めても）選択は残る
        // New behavior: the selection survives across strokes
        [Test]
        public void BeginStrokeKeepsPreviousSelectionAndResetsCategory()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            var block = new FakeDeleteTarget { Removable = true };
            selection.BeginStroke();
            selection.TryAddTarget(line, out _);
            Assert.IsFalse(selection.TryAddTarget(block, out var denied));
            Assert.AreEqual(LocalizationKeys.Ui.Delete.DifferentCategorySelection, denied);
            selection.BeginStroke();
            Assert.IsTrue(selection.TryAddTarget(block, out _));
            Assert.AreEqual(2, selection.Count);
            Assert.IsTrue(selection.Contains(line));
        }

        [Test]
        public void AddBoxTargetsIgnoresCategoryAndSkipsNonRemovable()
        {
            var selection = Create();
            var line = new FakeDeleteTarget { Removable = true, Category = BlockMasterElementExtension.ConnectionLineDestructionCategory };
            selection.BeginStroke();
            selection.TryAddTarget(line, out _);
            var block = new FakeDeleteTarget { Removable = true };
            var denied = new FakeDeleteTarget { Removable = false, DenyReason = LocalizationKeys.Ui.Delete.StationInternalRail };
            var result = selection.AddBoxTargets(new List<IDeleteTarget> { block, denied, block });
            Assert.AreEqual(1, result.Added);
            Assert.AreEqual(1, result.SkippedNonRemovable);
            Assert.AreEqual(2, selection.Count);
            Assert.AreEqual(0, denied.SetPreviewCount);
        }

        [Test]
        public void RevisionAdvancesOnAddCancelAndCommit()
        {
            var selection = Create();
            var target = new FakeDeleteTarget { Removable = true };
            var r0 = selection.Revision;
            selection.BeginStroke();
            selection.TryAddTarget(target, out _);
            var r1 = selection.Revision;
            selection.TryAddTarget(target, out _);
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

`using Client.Game.InGame.UI.UIState.State;` を `IDeleteTarget` のために足す。

- [ ] **Step 2: DeleteSelection を書く（`git mv` 後の全文）**

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
using Mooresmaster.Localization.Generated;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     箱追加の結果（足した件数と、撤去不可で飛ばした件数）
    ///     Result of a box add: how many were added and how many were skipped as non-removable
    /// </summary>
    public readonly struct DeleteBoxAddResult
    {
        public int Added { get; }
        public int SkippedNonRemovable { get; }

        public DeleteBoxAddResult(int added, int skippedNonRemovable)
        {
            Added = added;
            SkippedNonRemovable = skippedNonRemovable;
        }
    }

    /// <summary>
    ///     削除ツールの選択集合。なぞり・箱で積み上げ、確定時のUndo履歴記録も担う。ストロークを跨いで持続する
    ///     The delete tool's selection set: accumulated by strokes and boxes, persists across strokes, records undo on commit
    /// </summary>
    public class DeleteSelection
    {
        private readonly BuildOperationHistory _buildOperationHistory;
        private readonly IRemovalRestoreSender _restoreSender;

        // 論理削除キーで重複排除する（同一機械の複数メッシュ子などを1件に集約）
        // Dedupe by logical delete key so multiple mesh children of one machine collapse into one
        private readonly Dictionary<object, IDeleteTarget> _selectedTargets = new();
        private bool _strokeCanceled;

        // 最初の対象のカテゴリーで照準を固定（ストロークごと）
        // Fix aim to the first target's category (per stroke)
        public DeleteAimFilter AimFilter { get; private set; } = DeleteAimFilter.Frontmost;

        // 集合が変わるたびに進む。長押しの「対象が変わった」検知に使う
        // Advances whenever the set changes; the hold confirm uses it to detect a changed subject
        public int Revision { get; private set; }
        public int Count => _selectedTargets.Count;

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

        // 対象を選択へ追加する。削除可否・カテゴリー整合をまとめて判定し、追加不可なら拒否理由を返す（理由なし拒否はnull）
        // Add a target to the selection; judges removability and category together, returning a deny reason when rejected (null when there is none)
        public bool TryAddTarget(IDeleteTarget target, out LocalizationKey? denyReason)
        {
            denyReason = null;
            if (_strokeCanceled) return false;
            if (!target.IsRemovable(out denyReason)) return false;

            // セッションカテゴリーと異なるカテゴリーは追加しない（混在防止）
            // Reject a target whose category differs from the stroke category (prevents mixing)
            if (!AimFilter.Accepts(target))
            {
                denyReason = LocalizationKeys.Ui.Delete.DifferentCategorySelection;
                return false;
            }

            // 最初の追加でストロークのカテゴリーを固定する
            // Fix the stroke category on the first added target
            if (!AimFilter.IsCategoryRequired) AimFilter = DeleteAimFilter.Category(target.GetDestructionCategory());
            Add(target);
            return true;
        }

        // 箱の対象はカテゴリーを問わず足す。撤去不可は理由をログへ残して飛ばす
        // Box targets join regardless of category; non-removable ones are skipped with their reason logged
        public DeleteBoxAddResult AddBoxTargets(IReadOnlyList<IDeleteTarget> targets)
        {
            var added = 0;
            var skipped = 0;
            foreach (var target in targets)
            {
                if (!target.IsRemovable(out var denyReason))
                {
                    skipped++;
                    Debug.Log($"[DeleteBox] skipped non-removable target key={target.GetDeleteTargetKey()} reason={denyReason?.Key ?? "(none)"}");
                    continue;
                }
                if (Add(target)) added++;
            }
            return new DeleteBoxAddResult(added, skipped);
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
                if (target is Object unityTarget && unityTarget == null)
                {
                    Debug.LogWarning("[DragDelete] selected target was destroyed before cancel");
                    continue;
                }
                target.ResetMaterial();
            }
            _selectedTargets.Clear();
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
                if (target is Object unityTarget && unityTarget == null)
                {
                    Debug.LogWarning("[DragDelete] selected target was destroyed before commit");
                    continue;
                }
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
    }
}
```

- [ ] **Step 3: 既存テスト・シナリオの参照を置換する**

```bash
cd moorestech_client/Assets/Scripts/Client.Tests
sed -i '' -e 's/DragDeleteSelection/DeleteSelection/g' -e 's/BeginDrag()/BeginStroke()/g' -e 's/CanCommit()/IsStrokeAlive()/g' UIState/DragDeleteDenyReasonTest.cs UIState/DragDeleteSelectionCategoryTest.cs UIState/DragDelete/DestroyedSelectionTest.cs
grep -rn "DragDeleteSelection\|BeginDrag\|CanCommit" . ../Client.Game  # DeleteObjectService.cs 以外に残っていないこと（Service は Task 6 で書き換える）
sed -i '' 's/DragDeleteSelection\.CommitDelete/DeleteSelection.CommitDelete/g' ../../../../.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs
```

`DeleteObjectService.cs` は Task 6 で全面的に書き換えるが、このタスクのコンパイルを通すため最小限の置換（`DragDeleteSelection`→`DeleteSelection`、`BeginDrag()`→`BeginStroke()`、`CanCommit()`→`IsStrokeAlive()`）をここで行う。この時点では「離したら確定」のままでよい。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DeleteSelectionTest|DragDeleteDenyReasonTest|DragDeleteSelectionCategoryTest|DestroyedSelectionTest|RemoveOperationHistoryTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 5: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Tests/UIState .claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs
git commit -m "feat: 削除ツールの選択集合をDeleteSelectionへ改め、ストロークを跨いで持続・箱追加・Containsを足す"
```

---

### Task 4: DeleteHoldConfirm（F 長押し 2 秒と進捗バー）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm/DeleteHoldConfirm.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs`

**Interfaces:**
- Consumes: `ProgressBarState`（`Client.Game.InGame.UI.ProgressBar`。`Show()`/`Hide()`/`SetProgress(float)` は internal・同アセンブリ、`IsShown`/`CurrentProgress` は public）
- Produces:
  ```csharp
  public class DeleteHoldConfirm
  {
      public const float HoldSeconds = 2f;
      public DeleteHoldConfirm(ProgressBarState progressBar);
      public bool IsHolding { get; }
      // 毎フレーム呼ぶ。subjectKey が null なら長押し対象なし。満了したフレームだけ true を返し内部を戻す
      public bool Advance(bool isKeyHeld, object subjectKey, float deltaTime);
      public void Reset();
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
        [Test]
        public void 押し続けると進捗が伸び2秒で満了する()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsFalse(hold.Advance(true, "a", 1f));
            Assert.IsTrue(bar.IsShown);
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
            Assert.IsTrue(hold.Advance(true, "a", 1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void 途中で離すと進捗が0に戻りバーが消える()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, "a", 1f);
            Assert.IsFalse(hold.Advance(false, "a", 0.1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.Advance(true, "a", 1.5f));
            Assert.AreEqual(0.75f, bar.CurrentProgress, 0.001f);
        }

        [Test]
        public void 対象が変わると進捗が0からやり直しになる()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, 1, 1.5f);
            Assert.IsFalse(hold.Advance(true, 2, 1f));
            Assert.AreEqual(0.5f, bar.CurrentProgress, 0.001f);
        }

        [Test]
        public void 対象が無いときはバーを出さない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsFalse(hold.Advance(true, null, 1f));
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void 満了後は一度離すまで次の長押しを始めない()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            Assert.IsTrue(hold.Advance(true, "a", 2f));
            Assert.IsFalse(hold.Advance(true, "b", 5f));
            Assert.IsFalse(bar.IsShown);
            hold.Advance(false, "b", 0.1f);
            Assert.IsTrue(hold.Advance(true, "b", 2f));
        }

        [Test]
        public void Resetで進捗とバーを戻す()
        {
            var bar = new ProgressBarState();
            var hold = new DeleteHoldConfirm(bar);
            hold.Advance(true, "a", 1f);
            hold.Reset();
            Assert.IsFalse(bar.IsShown);
            Assert.IsFalse(hold.IsHolding);
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

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
        private object _subjectKey;
        private float _heldSeconds;
        private bool _awaitRelease;

        public bool IsHolding { get; private set; }

        public DeleteHoldConfirm(ProgressBarState progressBar)
        {
            _progressBar = progressBar;
        }

        // 毎フレーム呼ぶ。満了したフレームだけ true。離す・対象が変わる・対象なしで進捗は0へ戻る
        // Call every frame; true only on the frame the hold completes. Release, a changed subject, or no subject resets progress
        public bool Advance(bool isKeyHeld, object subjectKey, float deltaTime)
        {
            // 満了直後に押し続けても次の対象へ連続確定しない
            // Keeping the key down after completion must not chain into the next subject
            if (!isKeyHeld) _awaitRelease = false;
            if (!isKeyHeld || subjectKey == null || _awaitRelease)
            {
                Reset();
                return false;
            }

            if (!IsHolding || !Equals(_subjectKey, subjectKey)) Begin(subjectKey);

            _heldSeconds += deltaTime;
            _progressBar.SetProgress(_heldSeconds / HoldSeconds);
            if (_heldSeconds < HoldSeconds) return false;

            Reset();
            _awaitRelease = true;
            return true;
        }

        public void Reset()
        {
            if (IsHolding) _progressBar.Hide();
            IsHolding = false;
            _subjectKey = null;
            _heldSeconds = 0f;
        }

        private void Begin(object subjectKey)
        {
            if (!IsHolding) _progressBar.Show();
            IsHolding = true;
            _subjectKey = subjectKey;
            _heldSeconds = 0f;
        }
    }
}
```

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.DragDelete.DeleteHoldConfirmTest"`
Expected: ErrorCount 0 / 6 PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Confirm moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteHoldConfirmTest.cs
git commit -m "feat: 削除ツールのF長押し確定（2秒・進捗バー）を足す"
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
- Consumes: `CellRangeBox`, `CellRangeBoxVisualizer`（Task 1）、`DeleteBoxRule`, `DeleteBoxRailSampler`, `TrainRailObjectManager.EnumerateRailChains()`, `EntityObjectDatastore.EnumerateEntityObjects()`（Task 2）、`PlacementUnitCellResolver.TryGetCursorCell(Camera, int, out Vector3Int)`、`PlacementHeightOffset`（`Adjust`/`ResetToGround`/`Value`）、`PlacementHeightKeyInput.Apply(PlacementHeightOffset)`、`UiPointerHitTest.IsPointerOverAnyUi()`、`InputManager.Playable.ScreenLeftClick`、`HybridInput.GetKey(KeyCode.LeftShift)`、`BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary`、`BlockGameObject.BlockPosInfo`、`BlockGameObjectChild`、`TrainCarEntityObject`、`TrainCarEntityChildrenObject`、`TooltipLine(LocalizationKey, string[])`
- Produces:
  ```csharp
  public enum DeleteBoxPhase { Idle, SelectingEnd }
  public class DeleteBoxSelection { public DeleteBoxPhase Phase { get; } public Vector3Int StartCell { get; } public void SelectStart(Vector3Int cell); public void Clear(); }
  public class DeleteBoxTargetResolver
  {
      public DeleteBoxTargetResolver(BlockGameObjectDataStore blockGameObjectDataStore, EntityObjectDatastore entityObjectDatastore);
      public IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max);   // 新しい List を返す
  }
  public readonly struct DeleteBoxStep { public static DeleteBoxStep None { get; } public static DeleteBoxStep Completed(IReadOnlyList<IDeleteTarget> targets); public bool IsCompleted { get; } public IReadOnlyList<IDeleteTarget> Targets { get; } }
  public class DeleteBoxInput
  {
      public DeleteBoxInput(Camera mainCamera, DeleteBoxTargetResolver resolver);
      public bool IsSelectingEnd { get; }
      public DeleteBoxStep Update(List<TooltipLine> tooltipLines);   // 毎フレーム。終点確定のフレームだけ Completed
      public bool TryCancel();                                        // 終点待ちを畳めたら true
      public void Clear();                                            // 局面・高さ・表示を全て戻す
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
    ///     箱選択の局面と始点セル。終点は確定の瞬間に対象へ解決されるので保持しない
    ///     Box-selection phase and its start cell; the end cell resolves into targets at once, so it is not stored
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
using Client.Game.InGame.Entity;
using Client.Game.InGame.Entity.Object;
using Client.Game.InGame.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     AABBに入る削除対象（ブロック・レール edge・列車車両）の列を返す1本の問い合わせ。接続線は端点ブロックの撤去に巻き込まれるので含めない
    ///     One query returning the delete targets (blocks, rail edges, train cars) inside an AABB; connection lines ride on their endpoint blocks and are excluded
    /// </summary>
    public class DeleteBoxTargetResolver
    {
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private readonly EntityObjectDatastore _entityObjectDatastore;

        public DeleteBoxTargetResolver(BlockGameObjectDataStore blockGameObjectDataStore, EntityObjectDatastore entityObjectDatastore)
        {
            _blockGameObjectDataStore = blockGameObjectDataStore;
            _entityObjectDatastore = entityObjectDatastore;
        }

        public IReadOnlyList<IDeleteTarget> Resolve(Vector3Int min, Vector3Int max)
        {
            var targets = new List<IDeleteTarget>();
            var box = DeleteBoxRule.CreateBox(min, max);
            CollectBlocks();
            CollectRails();
            CollectTrainCars();
            return targets;

            #region Internal

            void CollectBlocks()
            {
                foreach (var block in _blockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary.Values)
                {
                    if (!DeleteBoxRule.IsBlockInBox(block.BlockPosInfo, box)) continue;
                    var child = block.GetComponentInChildren<BlockGameObjectChild>(true);
                    if (child == null)
                    {
                        Debug.LogWarning($"[DeleteBox] block has no BlockGameObjectChild: {block.BlockPosInfo.OriginalPos}");
                        continue;
                    }
                    targets.Add(child);
                }
            }

            void CollectRails()
            {
                foreach (var chain in TrainRailObjectManager.Instance.EnumerateRailChains())
                {
                    if (chain.IsRemoving) continue;
                    if (!DeleteBoxRailSampler.IntersectsCellBox(chain, min, max)) continue;
                    var rail = chain.GetComponentInChildren<DeleteTargetRail>(true);
                    if (rail == null)
                    {
                        Debug.LogWarning($"[DeleteBox] rail chain has no DeleteTargetRail: {chain.name}");
                        continue;
                    }
                    targets.Add(rail);
                }
            }

            void CollectTrainCars()
            {
                foreach (var entity in _entityObjectDatastore.EnumerateEntityObjects())
                {
                    if (entity is not TrainCarEntityObject car) continue;
                    if (!DeleteBoxRule.IsPointInBox(car.transform.position, min, max)) continue;
                    var child = car.GetComponentInChildren<TrainCarEntityChildrenObject>(true);
                    if (child == null)
                    {
                        Debug.LogWarning($"[DeleteBox] train car has no TrainCarEntityChildrenObject: {car.TrainCarInstanceId}");
                        continue;
                    }
                    targets.Add(child);
                }
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
    /// <summary>
    ///     1フレームの箱入力の結果。終点が確定したフレームだけ対象列を運ぶ
    ///     Result of one frame of box input; carries the targets only on the frame the end point is confirmed
    /// </summary>
    public readonly struct DeleteBoxStep
    {
        private static readonly IDeleteTarget[] Empty = System.Array.Empty<IDeleteTarget>();
        public static DeleteBoxStep None => new(false, Empty);
        public static DeleteBoxStep Completed(IReadOnlyList<IDeleteTarget> targets) => new(true, targets);

        public bool IsCompleted { get; }
        public IReadOnlyList<IDeleteTarget> Targets { get; }

        private DeleteBoxStep(bool isCompleted, IReadOnlyList<IDeleteTarget> targets)
        {
            IsCompleted = isCompleted;
            Targets = targets;
        }
    }

    /// <summary>
    ///     Shift+左クリックで始点、次の左クリックで終点を取る箱選択の入力・セル解決・可視化。高さは削除ツール専用のオフセットで持つ
    ///     Box-selection input (Shift+click start, click end), cell resolution and visuals; the height lives in a delete-tool-only offset
    /// </summary>
    public class DeleteBoxInput
    {
        private readonly Camera _mainCamera;
        private readonly DeleteBoxTargetResolver _resolver;
        private readonly DeleteBoxSelection _selection = new();
        private readonly PlacementHeightOffset _heightOffset = new();
        private CellRangeBoxVisualizer _visualizer;
        private bool _isShiftPressRegistered;
        private bool _isPressRegistered;

        public bool IsSelectingEnd => _selection.Phase == DeleteBoxPhase.SelectingEnd;

        public DeleteBoxInput(Camera mainCamera, DeleteBoxTargetResolver resolver)
        {
            _mainCamera = mainCamera;
            _resolver = resolver;
        }

        public DeleteBoxStep Update(List<TooltipLine> tooltipLines)
        {
            _visualizer ??= new CellRangeBoxVisualizer("DeleteBox", MaterialConst.NotPlaceableColor);
            PlacementHeightKeyInput.Apply(_heightOffset);
            var isShiftHeld = HybridInput.GetKey(KeyCode.LeftShift);
            var isClicked = TryConsumeClick(isShiftHeld);

            if (!PlacementUnitCellResolver.TryGetCursorCell(_mainCamera, _heightOffset.Value, out var cursorCell))
            {
                if (isClicked) Debug.Log("[DeleteBox] click refused: cursor has no placement surface");
                if (IsSelectingEnd) _visualizer.ShowSelectingStart(_selection.StartCell);
                else _visualizer.HideAll();
                return DeleteBoxStep.None;
            }

            if (!IsSelectingEnd) return UpdateIdle();
            return UpdateSelectingEnd();

            #region Internal

            // Shift付き押下→解放を始点クリック、素の押下→解放を終点クリックとして1回に畳む（UI上は無視）
            // Fold a Shift press→release into a start click and a plain press→release into an end click (ignored over UI)
            bool TryConsumeClick(bool shiftHeld)
            {
                if (InputManager.Playable.ScreenLeftClick.GetKeyDown && !UiPointerHitTest.IsPointerOverAnyUi())
                {
                    if (IsSelectingEnd) _isPressRegistered = true;
                    else if (shiftHeld) _isShiftPressRegistered = true;
                }
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return false;
                var isClick = (_isPressRegistered || _isShiftPressRegistered) && !UiPointerHitTest.IsPointerOverAnyUi();
                _isPressRegistered = false;
                _isShiftPressRegistered = false;
                return isClick;
            }

            DeleteBoxStep UpdateIdle()
            {
                // Shiftを押している間だけ始点候補を示す
                // Show the start candidate only while Shift is held
                if (isShiftHeld) _visualizer.ShowSelectingStart(cursorCell);
                else _visualizer.HideAll();
                if (isClicked) _selection.SelectStart(cursorCell);
                return DeleteBoxStep.None;
            }

            DeleteBoxStep UpdateSelectingEnd()
            {
                var (min, max) = CellRangeBox.Calc(_selection.StartCell, cursorCell);
                var targets = _resolver.Resolve(min, max);
                tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxTargetsInRange, new[] { targets.Count.ToString() }));
                if (targets.Count == 0) tooltipLines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteBoxEmptyRange));
                _visualizer.ShowSelectingEnd(_selection.StartCell, cursorCell, min, max);
                if (!isClicked) return DeleteBoxStep.None;

                // 空範囲は確定を拒み、ツールチップとログへ理由を残す
                // Refuse an empty range, leaving its reason in tooltip and log
                if (targets.Count == 0)
                {
                    Debug.Log($"[DeleteBox] end click refused: no delete targets in {min}-{max}");
                    return DeleteBoxStep.None;
                }
                Clear();
                return DeleteBoxStep.Completed(targets);
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

        public void Clear()
        {
            _selection.Clear();
            _heightOffset.ResetToGround();
            _isPressRegistered = false;
            _isShiftPressRegistered = false;
            _visualizer?.HideAll();
        }
    }
}
```

（`UiPointerHitTest` の名前空間は `Client.Game.InGame.Control`。`PlacementHeightKeyInput` は `Client.Game.InGame.BlockSystem.PlaceSystem.Common.Height`。実ファイルの `namespace` に合わせて `using` を調整する）

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.UIState.DragDelete.DeleteBoxSelectionTest"`
Expected: ErrorCount 0 / 2 PASS。`DeleteBoxInput.cs` が 200 行以下であることを確認。

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/Box moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/DeleteBoxSelectionTest.cs Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
git commit -m "feat: 削除ツールの箱選択（Shift+クリック2点・Q/E高さ・箱内対象の解決）を足す"
```

---

### Task 6: DeleteObjectService の司令塔化・DeleteObjectState 配線・キーヒント

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteHoverPreview.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs:18-33,57-63,120-132`
- Modify: `Localization/localization.csv`（`ui.keyHint.key.leftAltHold` 行の直後に `ui.keyHint.key.fHold`、`ui.keyHint.text.dragSelect` 行の直後に 4 行）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（再生成）
- Modify: `docs/adr/0032-key-hint-hud-owned-by-ui-state.md:52`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateKeyHintCatalogTest.cs`（破壊モード 7→11）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateFocusRestorationTest.cs:119`、`.../UIStateCameraInteractionTest.cs:92`、`.../RightShortPressTransitionTest.cs:68`（コンストラクタ引数追加）

**Interfaces:**
- Consumes: Task 3〜5 の `DeleteSelection`・`DeleteHoldConfirm`・`DeleteBoxInput`・`DeleteBoxTargetResolver`、`DeleteTargetRaycaster.AimAt(DeleteAimFilter)` → `DeleteAimResult`（`Target`、`GetDragDenyReason()`）、`IMouseCursorTooltip.Show(TooltipOwner, IReadOnlyList<TooltipLine>)`/`Hide(TooltipOwner)`、`InputManager.Playable.Interact.GetKey`
- Produces:
  ```csharp
  public class DeleteHoverPreview
  {
      public IDeleteTarget Current { get; }
      public void Update(IDeleteTarget hovered, DeleteSelection selection, List<TooltipLine> tooltipLines);  // ホバー替え・撤去不可理由行の追加
      public IDeleteTarget Release();   // 所有権を手放す（赤は戻さない）。戻り値は直前のホバー対象（無ければ null）
      public void Clear(DeleteSelection selection);  // 選択に入っていなければ赤を戻す
  }
  public class DeleteObjectService
  {
      public DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender, ProgressBarState progressBar, Camera mainCamera, DeleteBoxTargetResolver boxTargetResolver);
      public void Update(float deltaTime);
      public bool TryCancel();        // 箱 → 選択 の順に1段だけ畳む。畳む物が無ければ false
      public void ClearAll();         // モード離脱: 選択・箱・長押し・ホバー・ツールチップを全て片付ける
  }
  ```
  `DeleteObjectState(IRemovalRestoreSender, UiStateCameraPolicyService, BuildOperationHistory, BuildUndoService, PlacementTargetPickService, RightShortPressInputService, IMouseCursorTooltip, ProgressBarState, Camera, BlockGameObjectDataStore, EntityObjectDatastore)`
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

- [ ] **Step 3: DeleteHoverPreview を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    /// <summary>
    ///     単体ホバーの赤プレビューの所有者。選択済みの対象はホバーが外れても赤を戻さない
    ///     Owns the single-hover red preview; a selected target keeps its red when the hover leaves it
    /// </summary>
    public class DeleteHoverPreview
    {
        public IDeleteTarget Current { get; private set; }

        public void Update(IDeleteTarget hovered, DeleteSelection selection, List<TooltipLine> tooltipLines)
        {
            // ホバー対象が変われば旧を戻し（選択済みは除く）新を表示する
            // Swap preview when the hovered target changes (never un-red a selected one)
            if (Current != hovered)
            {
                if (Current != null && !selection.Contains(Current)) Current.ResetMaterial();
                Current = hovered;
                Current?.SetRemovePreviewing();
            }

            // 削除不可な対象は理由行だけ積む（理由なし拒否は何も出さない）
            // For a non-removable target only add the denial line (a reasonless denial adds nothing)
            if (Current != null && !Current.IsRemovable(out var reason) && reason.HasValue)
            {
                tooltipLines.Add(new TooltipLine(reason.Value));
            }
        }

        // 所有権を手放す（赤は戻さない。なぞり開始・暗黙選択の引き渡し用）
        // Hand off ownership without resetting the red (used when a stroke or implicit selection takes over)
        public IDeleteTarget Release()
        {
            var previous = Current;
            Current = null;
            return previous;
        }

        public void Clear(DeleteSelection selection)
        {
            if (Current != null && !selection.Contains(Current)) Current.ResetMaterial();
            Current = null;
        }
    }
}
```

- [ ] **Step 4: DeleteObjectService を書き換える（全文）**

```csharp
using System.Collections.Generic;
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
        private readonly DeleteHoverPreview _hover = new();
        private readonly IMouseCursorTooltip _tooltip;
        private readonly TooltipOwner _tooltipOwner = new();
        private readonly List<TooltipLine> _lines = new();
        private bool _isStroking;

        public DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip, IRemovalRestoreSender restoreSender, ProgressBarState progressBar, Camera mainCamera, DeleteBoxTargetResolver boxTargetResolver)
        {
            _selection = new DeleteSelection(buildOperationHistory, restoreSender);
            _box = new DeleteBoxInput(mainCamera, boxTargetResolver);
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

            // 箱は終点確定のフレームに対象列を渡してくる
            // The box hands over its targets on the frame the end point is confirmed
            var boxStep = _box.Update(_lines);
            if (boxStep.IsCompleted) AddBoxTargets(boxStep.Targets);

            HandleStrokeStart();
            if (_isStroking) UpdateStroke();
            else if (!InputManager.Playable.ScreenLeftClick.GetKey) _hover.Update(hovered, _selection, _lines);
            if (InputManager.Playable.ScreenLeftClick.GetKeyUp) _isStroking = false;

            UpdateHoldConfirm();

            if (_selection.HasSelection()) _lines.Add(new TooltipLine(LocalizationKeys.Ui.Tooltip.DeleteSelectedCount, new[] { _selection.Count.ToString() }));
            if (_lines.Count == 0) _tooltip.Hide(_tooltipOwner);
            else _tooltip.Show(_tooltipOwner, _lines.ToArray());

            #region Internal

            void AddBoxTargets(IReadOnlyList<IDeleteTarget> targets)
            {
                var result = _selection.AddBoxTargets(targets);
                Debug.Log($"[DeleteBox] added={result.Added} skippedNonRemovable={result.SkippedNonRemovable} selected={_selection.Count}");
            }

            // 箱の終点待ち中とShift押下中は左クリックを箱に譲り、なぞりは始めない
            // While the box awaits its end or Shift is held, the left click belongs to the box and no stroke starts
            void HandleStrokeStart()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyDown) return;
                if (UiPointerHitTest.IsPointerOverAnyUi()) return;
                if (_box.IsSelectingEnd || HybridInput.GetKey(KeyCode.LeftShift)) return;

                // 単体プレビューを戻してからストロークへ（選択済みは戻さない。追加時に再び赤になる）
                // Reset the single-hover preview before the stroke takes over (selected ones stay; adding re-reds it)
                _hover.Clear(_selection);
                _selection.BeginStroke();
                _isStroking = true;
            }

            void UpdateStroke()
            {
                // キャンセル済みストロークは離すまで不活性
                // A canceled stroke is inert until the button is released
                if (!_selection.IsStrokeAlive()) return;
                if (hovered == null)
                {
                    AddDenyLine(aim.GetDragDenyReason());
                    return;
                }
                if (!_selection.TryAddTarget(hovered, out var denyReason)) AddDenyLine(denyReason);
            }

            // 長押しの対象: 選択があれば集合（Revision）、無ければ撤去可能なホバー1件
            // Hold subject: the selection (by revision) when present, else the single removable hovered target
            void UpdateHoldConfirm()
            {
                object subject = null;
                if (_selection.HasSelection()) subject = _selection.Revision;
                else if (_hover.Current != null && _hover.Current.IsRemovable(out _)) subject = _hover.Current.GetDeleteTargetKey();

                if (!_hold.Advance(InputManager.Playable.Interact.GetKey, subject, deltaTime)) return;

                if (!_selection.HasSelection())
                {
                    // ホバー1件を暗黙の選択として足す。足せなければ赤だけ戻す（撤去可否が直前に変わった場合）
                    // Add the hovered target as the implicit selection; if it cannot join, just reset its red (removability changed meanwhile)
                    var implicitTarget = _hover.Release();
                    _selection.BeginStroke();
                    if (!_selection.TryAddTarget(implicitTarget, out _)) implicitTarget.ResetMaterial();
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

        // ESC/右短押し: 終点待ちの箱 → 選択の全解除 の順に1段だけ畳む。畳む物が無ければ false（呼び出し側がモード終了を判断）
        // ESC / right short press: fold the pending box, else clear the selection; false when nothing to fold (caller decides to exit)
        public bool TryCancel()
        {
            if (_box.TryCancel()) return true;
            if (!_selection.HasSelection()) return false;
            _selection.CancelSelection();
            _hold.Reset();
            return true;
        }

        // モード離脱: 選択・箱・長押し・ホバー・ツールチップを全て片付ける
        // Mode exit: clear selection, box, hold, hover preview and tooltip
        public void ClearAll()
        {
            _selection.CancelSelection();
            _box.Clear();
            _hold.Reset();
            _hover.Clear(_selection);
            _tooltip.Hide(_tooltipOwner);
            _isStroking = false;
        }
    }
}
```

- [ ] **Step 5: DeleteObjectState を配線する**

コンストラクタ・フィールド:

```csharp
        public DeleteObjectState(IRemovalRestoreSender restoreSender, UiStateCameraPolicyService cameraPolicyService, BuildOperationHistory buildOperationHistory, BuildUndoService buildUndoService, PlacementTargetPickService placementTargetPickService, RightShortPressInputService rightShortPressInputService, IMouseCursorTooltip tooltip, ProgressBarState progressBar, Camera mainCamera, BlockGameObjectDataStore blockGameObjectDataStore, EntityObjectDatastore entityObjectDatastore)
        {
            _cameraPolicyService = cameraPolicyService;
            var boxTargetResolver = new DeleteBoxTargetResolver(blockGameObjectDataStore, entityObjectDatastore);
            _deleteObjectService = new DeleteObjectService(buildOperationHistory, tooltip, restoreSender, progressBar, mainCamera, boxTargetResolver);
            _buildUndoService = buildUndoService;
            _placementTargetPickService = placementTargetPickService;
            _rightShortPressInputService = rightShortPressInputService;
        }
```

`GetNextUpdate` 内: `_deleteObjectService.Update();` → `_deleteObjectService.Update(Time.deltaTime);`、`HandleTransition` 内の `!_deleteObjectService.TryCancelSelection()` → `!_deleteObjectService.TryCancel()`、ESC コメントを「ESC/右短押しはまず終点待ちの箱、次に選択の全解除に使い、どちらも無ければ破壊モードを抜ける」へ。`OnExit` の `_deleteObjectService.CancelSelection();` → `_deleteObjectService.ClearAll();`。`using Client.Game.InGame.Block;`、`using Client.Game.InGame.Entity;`、`using Client.Game.InGame.UI.ProgressBar;`、`using Client.Game.InGame.UI.UIState.State.DragDelete.Box;` を足す。

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

- [ ] **Step 6: 既存テストのコンストラクタ呼び出しを更新する**

`UIStateFocusRestorationTest.cs:119`、`UIStateCameraInteractionTest.cs:92`、`RightShortPressTransitionTest.cs:68` の `new DeleteObjectState(..., <tooltip>)` の末尾に `, new ProgressBarState(), CreateObject("DeleteTestCamera", true).AddComponent<Camera>(), null, null` を足す（`BlockGameObjectDataStore`・`EntityObjectDatastore` は箱の終点確定時にしか触られないので null でよい。`CreateObject` は `UIStateTestFixtureBase` の既存ヘルパー。無いフィクスチャでは `new GameObject("DeleteTestCamera").AddComponent<Camera>()` を使い TearDown で `Object.DestroyImmediate`）。`using Client.Game.InGame.UI.ProgressBar;` を足す。

- [ ] **Step 7: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.UIState"`
Expected: ErrorCount 0 / 全 PASS。`DeleteObjectService.cs`・`DeleteObjectState.cs` が 200 行以下。

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State moorestech_client/Assets/Scripts/Client.Tests/UIState Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts docs/adr/0032-key-hint-hud-owned-by-ui-state.md
git commit -m "feat: 削除ツールを選択積み上げ→F長押し確定へ変え、箱選択とキーヒントを配線する"
```

---

### Task 7: unityプレイ録画テスト（新規シナリオ＋既存 3 シナリオの更新）

**Files:**
- Create: `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/delete-tool-box-select-hold-confirm.cs`
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/building/build-undo-ctrl-z.cs`（削除手順）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/train/delete-tool-pier-removal-undo-rail.cs`（削除手順）
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
- `train/delete-tool-pier-removal-undo-rail.cs`、`building/build-undo-ctrl-z.cs`: 同様に、削除ツールでクリック／ドラッグした直後の `MouseButtonUp(0)` の後へ `await HoldDeleteConfirm();`（`build-undo-ctrl-z.cs` はローカル関数が `p` を引数で受ける形式なので `HoldDeleteConfirm(PlaytestDriver p)` に合わせる）。

- [ ] **Step 2: 新規シナリオを書く**

```csharp
// 削除ツールの新方式: なぞり選択は離しても残り、F長押し(2秒)で確定。Shift+クリック2点の箱で範囲選択。ESCで全解除。Ctrl+Zで復元
// New delete-tool flow: stroke selection survives release, F hold (2s) confirms; Shift+click two-point box selects a range; ESC clears; Ctrl+Z restores
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using Client.Playtest;
using Client.Playtest.Input;
using Client.Playtest.Operations;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
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
    await AimAtCell(cells[0]);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(10); SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(1.0f);
    p.Assert(p.GetBlock(cells[0]) != null, "Step1: 離しただけでは撤去されない");
    p.Assert(TooltipText().Contains("1"), $"Step1: ツールチップに選択件数1が出る 実際:{TooltipText()}");
    await p.Screenshot("02-stroke-kept");

    // Step 2: F長押しで確定
    // Step 2: F hold confirms
    p.Note("Step 2: F長押し2秒で選択を削除");
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds * 0.5f);
    await p.Screenshot("03-holding");
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds * 0.5f + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[0]) == null, 15f, "Step2: F長押し満了でチェスト1が撤去される");
    await p.Screenshot("04-deleted");

    // Step 3: Shift+クリック2点の箱でチェスト2・3を選び、F長押しで一括削除
    // Step 3: box-select chests 2 and 3 with Shift+click twice, then confirm with F
    p.Note("Step 3: Shift+クリックで始点、クリックで終点→範囲内2件→F長押し");
    await AimAtGround(cells[1] + new Vector3Int(-1, 0, -1));
    SemanticInput.KeyDown(Key.LeftShift); await UniTask.DelayFrame(3);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(5); SemanticInput.MouseButtonUp(0); await UniTask.DelayFrame(3);
    SemanticInput.KeyUp(Key.LeftShift);
    await p.Until(() => GameObject.Find("DeleteBoxStartMarker") != null && GameObject.Find("DeleteBoxStartMarker").activeSelf, 5f, "Step3: 始点マーカーが出る");
    await AimAtGround(cells[2] + new Vector3Int(1, 0, 1));
    await p.Until(() => TooltipText().Contains("2"), 5f, $"Step3: 範囲内の対象2件 実際:{TooltipText()}");
    await p.Screenshot("05-box-end-hover");
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(5); SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(0.5f);
    p.Assert(p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, "Step3: 終点確定だけでは撤去されない");
    await p.Screenshot("06-box-selected");

    // Step 4: ESCで全解除→再び箱→F長押し
    // Step 4: ESC clears, box again, then F hold
    p.Note("Step 4: ESCで全解除（赤が戻る）→もう一度箱で選び直しF長押し");
    await p.PressKey(Key.Escape);
    await p.WaitSeconds(0.5f);
    p.Assert(p.CurrentUiState == UIStateEnum.DeleteBar, "Step4: 選択があるときのESCは破壊モードに留まる");
    p.Assert(!TooltipText().Contains("選択中") && !TooltipText().Contains("Selected"), $"Step4: 全解除で件数行が消える 実際:{TooltipText()}");
    await AimAtGround(cells[1] + new Vector3Int(-1, 0, -1));
    SemanticInput.KeyDown(Key.LeftShift); await UniTask.DelayFrame(3);
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(5); SemanticInput.MouseButtonUp(0); await UniTask.DelayFrame(3);
    SemanticInput.KeyUp(Key.LeftShift);
    await AimAtGround(cells[2] + new Vector3Int(1, 0, 1));
    await p.Until(() => TooltipText().Contains("2"), 5f, "Step4: 範囲内の対象2件");
    SemanticInput.MouseButtonDown(0); await UniTask.DelayFrame(5); SemanticInput.MouseButtonUp(0);
    await p.WaitSeconds(0.5f);
    SemanticInput.KeyDown(Key.F);
    await p.WaitSeconds(DeleteHoldConfirm.HoldSeconds + 0.3f);
    SemanticInput.KeyUp(Key.F);
    await p.Until(() => p.GetBlock(cells[1]) == null && p.GetBlock(cells[2]) == null, 15f, "Step4: 箱の2件がまとめて撤去される");
    await p.Screenshot("07-box-deleted");

    // Step 5: Ctrl+Zで2件が戻る
    // Step 5: Ctrl+Z restores both
    p.Note("Step 5: Ctrl+Zで箱の2件が1バッチで戻る");
    foreach (var (key, down) in new[] { (Key.LeftCtrl, true), (Key.Z, true), (Key.Z, false), (Key.LeftCtrl, false) }) { if (down) SemanticInput.KeyDown(key); else SemanticInput.KeyUp(key); await UniTask.DelayFrame(3); }
    await p.Until(() => p.GetBlock(cells[1]) != null && p.GetBlock(cells[2]) != null, 15f, "Step5: Ctrl+Zで2件とも復元される");
    await p.Screenshot("08-undone");

    #region Internal

    string TooltipText()
    {
        var presentation = Client.Game.InGame.Context.ClientDIContext.DIContainer.Resolve<MouseCursorTooltipState>().GetPresentation();
        return string.Join("|", presentation.Lines.Select(line => line.Key.Key + ":" + string.Join(",", line.TextParams ?? System.Array.Empty<string>())));
    }

    // ブロックの各コライダー中心のうち削除ツールの実レイキャストが当該ブロックを返す点を照準する
    // Aim at the collider center where the real delete-tool raycast returns that block
    async UniTask AimAtCell(Vector3Int pos)
    {
        var block = ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos);
        foreach (var point in block.GetComponentsInChildren<Collider>().Select(c => c.bounds.center).OrderBy(c => Vector3.Distance(c, Camera.main.transform.position)).ToList())
        {
            await p.AimAt(point);
            var target = Client.Game.InGame.Control.DeleteTargetRaycaster.AimAt(DeleteAimFilter.Frontmost).Target;
            if (target is Client.Game.InGame.Block.BlockGameObjectChild child && ReferenceEquals(child.BlockGameObject, block)) return;
        }
        p.Assert(false, $"AimAtCell: {pos} を照準できない");
    }

    // 地面セルの中心（上面）を照準する。1x1設置と同じ解決で上のセルが選ばれる
    // Aim at the top face of a ground cell; the 1x1 placement rule resolves to the cell above it
    async UniTask AimAtGround(Vector3Int cellAboveGround)
    {
        await p.AimAt(new Vector3(cellAboveGround.x + 0.5f, cellAboveGround.y - 0.02f, cellAboveGround.z + 0.5f));
    }

    #endregion
});
```

`MouseCursorTooltipState` の DI 解決は `BlueprintCopyPasteScenarioChecks` が受け取っている形（`PlaytestDriver` 経由）があればそれに合わせる。`TooltipText()` はローカライズ前のキーと引数を結合するので、件数の照合は `TextParams` に数字が入ることで行う（上の `Contains("1")` / `Contains("2")` は `TextParams` の数字を拾う。`"選択中"`/`"Selected"` の不在判定は `LocalizationKeys.Ui.Tooltip.DeleteSelectedCount.Key` の不在判定へ置き換えてよい）。

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

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 7 の録画シナリオ 4 本を反映後のバイナリで再実施してから完了とする。

### Task 9: セッション終了可能状態にする（必須）

- [ ] pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 作成後に `moores-wt rm delete-tool-hold-confirm-box-select` で worktree を畳む。bd `moorestech-wmhf8` を close する。

---

## 判断記録（ADR）

設計 ADR: `docs/adr/0079-delete-tool-accumulates-selection-and-confirms-with-f-hold-and-box-select.md`（ユーザー裁定 10 件は `.decisions/2026-10-16-削除ツール*.md`）。前提: ADR 0076、ADR 0046、ADR 0032。

planning 中に生じた判断:

- `EntityObjectDatastore.cs` へ `EnumerateEntityObjects()`（読み取り専用の列挙口）を足す。箱選択が車両を走査する最小の口で、変更系は露出しない。出所: agent前提（`BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary` を BP コピーの計数が読む前例と同じ読み取り面）
- `TrainRailObjectManager.cs` へ `EnumerateRailChains()` を足す（`FindObjectsByType` で設置プレビューのチェーンまで拾うのを避ける）。出所: agent前提
- レール曲線のサンプルは `BezierRailChain.cs`（197 行）に足さず、子 `BezierRailMesh` の制御点を `BezierRailMeshData.EvaluatePosition` で評価する `DeleteBoxRailSampler` を新設する。出所: agent前提（200 行制限）
- 選択モデルは `DragDeleteSelection` → `DeleteSelection` へ改名（持続する選択集合になり「ドラッグ」限定でなくなるため。名前は実処理と一致させる）。ディレクトリ `DragDelete/` と名前空間は据え置く。出所: agent前提
- 箱の高さオフセットは DI の `PlacementHeightOffset` シングルトン（設置 HUD の正）を使わず、削除ツール専用のインスタンスを `DeleteBoxInput` が持つ。設置モードの高さ・HUD 表示へ漏らさないため。出所: agent前提
- Q/E は箱の始点・終点どちらのセル高さにも効く（BP コピーと同じ）。ADR 0079 の「終点待ち中だけ」はこれに改める。出所: agent前提（BlueprintCopySystem の挙動に揃える）
- 箱の始点候補マーカーは Shift を押している間だけ出す（箱モードに入っていることの合図）。出所: agent前提
- 削除箱の範囲色は `MaterialConst.NotPlaceableColor`（赤系）、BP コピーは従来どおり `PlaceableColor`。出所: agent前提
- 長押し満了後は F を一度離すまで次の長押しを始めない（押し続けた指がホバー対象を 2 秒後に消す連鎖を防ぐ）。出所: agent前提
- 長押し中にストロークを続けて対象を足すと `Revision` が変わり進捗が 0 に戻る（ADR の「選択集合が変わればリセット」どおり）。出所: ADR 0079
- 箱の対象解決（`DeleteBoxTargetResolver`）と入力（`DeleteBoxInput`）は EditMode ユニットテストで直接は検証しない（`BlockGameObjectDataStore`・レール・カメラのレイキャストが要る）。純関数 `DeleteBoxRule`・モデル `DeleteBoxSelection`・`DeleteHoldConfirm`・`DeleteSelection` をユニットテストで固め、配線は Task 7 の unityプレイ録画テストで通す。EditModeInPlayingTest は録画テストが上位互換のため作らない。出所: agent前提
- 既存の録画シナリオ 3 本（連結線切断・橋脚撤去・Ctrl+Z）は削除手順に F 長押しを足して新方式へ追従させる。出所: R17（ADR 0079 の帰結）
- `ProgressBarState` の `Show/Hide/SetProgress` は internal だが `DeleteHoldConfirm` は同アセンブリ（Client.Game）なので採掘と同じく直接呼ぶ。出所: agent前提（MiningProgressState 前例）
