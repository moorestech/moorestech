# クリーンルーム フェーズ4（製造機統合：最大グレード天井 ＋ down-bin ＋ Invalid停止）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` 等で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 blockType／新規 recipe／新規アイテムを認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」「ItemId/BlockId が引けない」で失敗したら uloop で Unity 再起動してから再試行。
> - 「Domain Reload in progress」エラーが出たら45秒待って再試行。
> - blockType／recipe／item スキーマ追加・SourceGenerator トリガの手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル（`.cs`／`.json`／`.yml`）編集時は AGENTS.md の「文字化け防止ワークフロー」を順守（編集前後でエンコーディング確認、`縺`/`繧`/`繝` 連続が出たら破棄して読み直し）。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースのパターンから書いているが、メソッド名・名前空間・引数順は推定を含む。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** 半導体製造機（露光装置）の出力を、その機械が入っているクリーンルームのクラスに連動させる。(a) 部屋が **Invalid** なら稼働停止（電力0と同じ「止まるが壊れない」）、Degraded＋猶予中は稼働継続。(b) クラスが課す**最大グレードの天井**で出力チップ Lv をクランプ。(c) 汚れに応じ確率で**下位 Lv へ格下げ（down-bin）**。グレードは独立 ItemId（`ICチップ_Lv1..Lv4`）で表現し、抽選はフェーズAの決定的乱数・アップグレード設計書§7.2 の順序に準拠する。

**Architecture:**
- グレード＝独立 ItemId。`ItemStack` に品質フィールドは無い（確認済み）ので、半導体チップを `ICチップ_Lv1..Lv4` の4独立アイテム（**決定的 GUID**）としてレベルファミリーで定義する。露光装置レシピは**基礎出力レベル分布テーブル**（`Lv1:p1 / … / Lv4:p4`）を持つ。
- レベル決定はアップグレード設計書 §7.2 の「③各出力に品質（レベル）を決定」の中核として実装する。合成順序を**固定**する: **室Invalid停止ゲート → （稼働）→ 完了時：天井クランプ → 基礎分布抽選 → [品質シフト：将来アップグレードBの予約穴・本フェーズは中立] → down-bin格下げ → Lv確定**。
- 乱数は**フェーズAの決定的乱数**（保存される `_processedCycleCount` ＋ `_blockInstanceId` 由来。共有 static Random 禁止）を流用。ただしレベル抽選と down-bin と生産性追加産出が同一シードを引くと相関するため、**salt 引数**でサブストリームを decorrelate する（productivity / level-draw / down-bin に別定数）。これによりテストは決定的かつ再現可能。
- **疎結合境界（asmdef 依存方向の厳守）:** `Game.Block` は `Game.CleanRoom` を**直接参照しない**。製造機が室状態を問い合わせる口 `ICleanRoomMachineGate` は `Game.Block.Interface` に置き、実装 `CleanRoomMachineGate` は `Game.CleanRoom` に置く。製造機は `ServerContext.GetService<ICleanRoomMachineGate>()`（既存の Train 系コンポーネントが採る同一機構）でインターフェースだけを見る。
- **共有コンポーネントのスコープ厳守（最重要）:** `VanillaMachineProcessorComponent` / `VanillaMachineOutputInventory` は**全機械共通**。停止ゲートとグレード差し替えを無条件で挿すと、クリーンルーム外の竃・組立機まで止まり既存テストが全崩壊する。したがって「この機械はクリーンルーム半導体機械か」を判定する**単一の discriminator** を導入し、停止ゲートとグレード差し替えの**両方が同じ discriminator** を使う（乖離防止）。非対象機械は常に稼働・常にベース出力。

**Tech Stack:** C# (Unity, moorestech_server), R3/UniRx `IObservable`（`GameUpdater.UpdateObservable`）, NUnit (Server.Tests), Mooresmaster Source Generator（`blocks.yml`/`items.yml`/`machineRecipes` 系 → 各 Module）。

---

## 前提（このプランが乗る土台）

| 前提 | 状態 | このプランへの影響 |
|---|---|---|
| **フェーズ1完了** | `Game.CleanRoom` asmdef ＋ `CleanRoom`（Id, `Cells`, V, S）＋ `CleanRoomDetectionSystem.TryGetRoomContainingBlock` | `CleanRoomMachineGate` が室を引く土台。**未マージなら本プランの統合テスト（実部屋）は成立しない** |
| **フェーズ2完了** | `CleanRoomPurityService` ＋ `CleanRoomPurityState`（クラス／`CleanRoomRoomStatus` Valid/Degraded/Invalid／猶予）＋ `CleanRoomClass` 列挙 | `CleanRoomMachineGate` が Status とクラスを引く先 |
| **フェーズ3完了** | 清浄機・汚染源で実際にクラスが動く | 実部屋でクラスA/Cを作る統合テストの前提 |
| **アップグレード フェーズA完了** | `VanillaMachineProcessorComponent` に `_processedCycleCount`／`_blockInstanceId`／`_currentEffect`／`DeterministicRoll`／`MachineModuleEffect` の Quality 穴／仮想容量予約パターンが**実装済み** | 本プランの決定的乱数・容量予約・品質穴は**フェーズAの流儀をそのまま継ぐ**。フェーズA未マージなら、本プランの RNG/予約コードは該当フィールドを前提に書く（下記注記参照） |

> ⚠ **ワークツリー注意:** 本プラン作成時点の作業ツリーには phase-1（`Game.CleanRoom`）も phase-A の機械変更も**未マージ**。本プランはこれらがマージ済みの前提で記述する。実装着手前に `git log`/ファイル存在で土台の有無を確認し、無ければ先に当該フェーズを完了させること。`DeterministicRoll`/`_processedCycleCount`/`_blockInstanceId` が機械に無ければ、フェーズA Task A3-4 のパターンを先に入れる。

---

## File Structure（フェーズ4で作成/変更するファイル）

**マスタ（レベルファミリー＋分布テーブル＋クラス天井/down-bin率）**
- Modify: `VanillaSchema/items.yml`（または既存のレベルファミリー用スキーマ）— `ICチップ` を `baseItem`／`maxLevel:4`／`combineCount` のレベルファミリーとして定義（SourceGenerator が `ICチップ_Lv1..Lv4` ＋段間合成レシピを生成。決定的 GUID）
- Modify: `VanillaSchema/machineRecipes.yml`（露光装置レシピ系スキーマ）— 出力に**レベル分布テーブル**（`levelDistribution: [{level, weight}…]`）を持つ枠を追加
- Modify: `VanillaSchema/cleanRoomClasses.yml`（フェーズ2で新規作成済み）— 既に最大グレード・down-bin率を持つ前提（フェーズ2で定義）。本フェーズはアクセサ確認のみ
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ文字列を変更
- Modify: テスト用 mod
  - `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json` — `ICチップ_Lv1..Lv4` 相当の4アイテム
  - `.../master/machineRecipes.json` — 露光装置レシピ＋レベル分布テーブル
  - `.../master/blocks.json` — 露光装置ブロック（半導体機械 discriminator が立つもの）
  - `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestItemId.cs` — `ICチップ_Lv1..Lv4` の ItemId アクセサ追加

**製造機フック（Game.Block ／ Game.Block.Interface）**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomMachineGate.cs` — 製造機が参照する室状態クエリ口（`CanOperate` / `ResolveClass`）。`CleanRoomClass` も `Game.Block.Interface` 側で参照できる必要があるため、フェーズ2で `CleanRoomClass` を `Game.Block.Interface` に置くか、ゲート戻り値を `int`/独立 enum にするかを下記 Task 2 で確定
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomGradeResolver.cs` — グレード解決の疎結合口（出力 ItemId 差し替え）。`Game.Block` 側は実装を知らずインターフェース越しに呼ぶ
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` — Idle 開始条件＋Processing 進捗に停止ゲートを AND（半導体機械限定）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs` — `IsAllowedToOutputItem`（天井Lvで容量予約）＋ `InsertOutputSlot`（出力 ItemId をグレード解決で差し替え）

**クリーンルーム側の実装（Game.CleanRoom）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomMachineGate.cs` — `ICleanRoomMachineGate` 実装。機械の `BlockPositionInfo.MinPos..MaxPos` 全占有セルが**同一の有効室**に入るかで室を引き、Status＋クラスを返す
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomGradeResolver.cs` — `ICleanRoomGradeResolver` 実装。クラス→天井クランプ→基礎分布抽選→down-bin→出力 ItemId。**純粋・決定的**（seed注入）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/SemiconductorLevelFamily.cs` — `ICチップ_Lv1..Lv4` の ItemId ↔ Lv 対応と分布テーブル参照（半導体限定）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — `CleanRoomMachineGate` / `CleanRoomGradeResolver` を `ICleanRoomMachineGate` / `ICleanRoomGradeResolver` として DI 登録（eager 不要、機械から `GetService` で引く）。`Game.CleanRoom` への asmdef 参照を追加

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomGradeResolverTest.cs` — 純関数テスト（クラス別 down-bin 率・天井・決定性）
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineGateTest.cs` — 停止ゲート（fake gate で配線）＋実部屋統合

> 各 `.cs`／`.asmdef` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: 半導体レベルファミリー＋露光装置の基礎レベル分布をマスタに定義

半導体チップを Lv1〜Lv4 の独立 ItemId（決定的 GUID）として定義し、露光装置レシピに基礎レベル分布テーブルを持たせる。テスト mod にも同等を追加する。本タスクはコード生成＋データ追加のみ（検証は Task 4/6 のテスト）。`edit-schema` スキルの手順に従うこと。

**Files:**
- Modify: `VanillaSchema/items.yml`（レベルファミリー用フィールド。既存のレベルファミリー機構があるならそれに合わせる）
- Modify: `VanillaSchema/machineRecipes.yml`（出力レベル分布テーブル枠）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: テスト mod の `items.json` / `machineRecipes.json` / `blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestItemId.cs`

- [ ] **Step 1: 既存スキーマ構造を確認**

`VanillaSchema/items.yml` と `VanillaSchema/machineRecipes.yml` を読み、(a) レベルファミリー定義の既存機構（`baseItem`/`maxLevel`/`combineCount`）の有無、(b) 機械レシピ `OutputItems` の現スキーマ（`ItemGuid`/`Count`）を確認する。アップグレード設計書 §4.3 の方式に合わせる。

- [ ] **Step 2: `ICチップ` をレベルファミリーとして定義**

`items.yml` に `ICチップ` の `baseItem` 定義を追加（`maxLevel: 4`、`combineCount` は仮 4）。SourceGenerator が `ICチップ_Lv1..ICチップ_Lv4` の独立アイテム（**決定的 GUID＝`baseGuid+level` 派生 or 固定値**。ランダム GUID 禁止）と段間合成レシピを自動生成する。

> セーブは `itemGuid + count` のみで識別するため、Lv派生 GUID は必ず決定的に。毎回変わるとセーブ即破損（アップグレード設計書 §4.3）。

- [ ] **Step 3: 露光装置レシピに基礎レベル分布テーブルを追加**

`machineRecipes.yml` の出力定義に、レベル分布を表す枠を追加。最小形:

```yaml
      levelDistribution:
        type: array
        items:
          type: object
          properties:
            - name: level
              type: integer
            - name: weight
              type: number
```

露光装置レシピの出力 `ICチップ` に `levelDistribution: [{level:1, weight:0.70}, {level:2, weight:0.20}, {level:3, weight:0.08}, {level:4, weight:0.02}]` を持たせる（初期値。テストで固定）。分布が無いレシピは「非レベル出力＝従来通り」とする。

- [ ] **Step 4: SourceGenerator をトリガ**

`_CompileRequester.cs` の `dummyText` 定数を変更:

```csharp
private const string dummyText = "regenerate-cleanroom-phase4";
```

- [ ] **Step 5: テスト mod にデータ追加**

`items.json` に `ICチップ_Lv1..Lv4` の4アイテム（決定的 GUID）。`machineRecipes.json` に露光装置レシピ＋上記分布テーブル。`blocks.json` に露光装置ブロック（Task 2 の discriminator が立つ blockType／パラメータ。下記 Task 2 で discriminator を確定してから値を入れる）。AGENTS.md 文字化け防止ワークフロー順守。

`ForUnitTestItemId.cs` にアクセサを追加（既存パターン＝`MasterHolder.ItemMaster.GetItemId(Guid.Parse(...))`）:

```csharp
        public static ItemId IcChipLv1 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000001"));
        public static ItemId IcChipLv2 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000002"));
        public static ItemId IcChipLv3 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000003"));
        public static ItemId IcChipLv4 => MasterHolder.ItemMaster.GetItemId(Guid.Parse("3a000000-0000-0000-0000-000000000004"));
```

> GUID 値は実際に `items.json` / レベルファミリー生成で確定する GUID に合わせること（決定的に）。

- [ ] **Step 6: 再生成を確認**

Run: Unity 再起動 → `uloop compile --project-path ./moorestech_client`
Expected: 成功。`ICチップ_Lv1..Lv4` のアイテムと露光装置レシピがロードでき、`ForUnitTestItemId.IcChipLv1` 等が引ける（Task 4 のテストで参照確認）。

> 新規アイテム／レシピのため Unity 再起動が要る場合がある。型/ItemId 未検出なら再起動して再試行。

- [ ] **Step 7: Commit**

```bash
cd ~/moorestech
git add VanillaSchema/items.yml VanillaSchema/machineRecipes.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Tests.Module/TestMod/
git commit -m "feat(cleanroom): 半導体チップ Lv1-4 レベルファミリーと露光装置の基礎レベル分布を定義"
```

---

## Task 2: 半導体機械 discriminator ＋ ゲート/解決のインターフェース（Game.Block.Interface）

製造機が「自分はクリーンルーム半導体機械か」を判定する**単一 discriminator** と、室状態クエリ口・グレード解決口を `Game.Block.Interface` に定義する。これにより `Game.Block` は `Game.CleanRoom` を直接参照しない。検証は Task 3/4 のテストで行う（ここではコンパイルのみ）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomMachineGate.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomGradeResolver.cs`

- [ ] **Step 1: discriminator の方式を確定**

候補2つ。**停止ゲートとグレード差し替えで同じ discriminator を使う**（乖離防止）こと。

- (A) **レシピ出力がレベルファミリーに属するか**: `CleanRoomGradeResolver`/`SemiconductorLevelFamily` が「このレシピは `ICチップ` 分布を持つか」を判定でき、持つレシピだけを半導体機械扱いにする。停止ゲートも「この機械のレシピがレベル分布を持つ」で発火。**推奨**（マスタ駆動・追加 blockType 不要）。
- (B) blockType マーカー（露光装置専用 blockType にマーカーコンポーネント）。明示的だが blockType 追加が要る。

本プランは **(A) レシピ分布駆動**を採る。`ICleanRoomGradeResolver.HasLevelDistribution(MachineRecipeMasterElement)` を判定口にし、停止ゲート適用も同じ判定を使う。

- [ ] **Step 2: `ICleanRoomMachineGate` を作成**

`Game.Block.Interface/Component/ICleanRoomMachineGate.cs`:

```csharp
using Game.Block.Interface;

namespace Game.Block.Interface.Component
{
    // 製造機が室の状態を問い合わせる疎結合境界。Game.Block が Game.CleanRoom を直接参照しないための口。
    // Loose-coupled gate so the machine asks the room without Game.Block depending on Game.CleanRoom.
    public interface ICleanRoomMachineGate
    {
        // 室が Invalid なら false。Degraded/猶予中・室外の半導体機械でない場合は true（=停止しない）。
        // False only when the machine's room is Invalid; Degraded/grace and non-gated machines return true.
        bool CanOperate(BlockInstanceId machine);

        // 天井クランプ＋down-bin に使う室クラス（0=最良 A …）。室外/無効は最悪クラス相当を返す。
        // Room class used for ceiling clamp and down-bin (0 = best A …).
        int ResolveClass(BlockInstanceId machine);
    }
}
```

> `CleanRoomClass` 列挙は `Game.CleanRoom` 側にあり `Game.Block.Interface` から参照できないため、ゲート戻り値は **`int`（クラス序数）**で受け渡す。解決側（`CleanRoomGradeResolver`）が `int` ↔ `CleanRoomClass` を変換する。フェーズ2で `CleanRoomClass` を `Game.Block.Interface` へ移動済みなら直接 enum を使ってよい（フェーズ2の配置を確認）。

- [ ] **Step 3: `ICleanRoomGradeResolver` を作成**

`Game.Block.Interface/Component/ICleanRoomGradeResolver.cs`:

```csharp
using Core.Item.Interface;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Interface.Component
{
    // 出力 ItemStack 生成直前に呼ばれ、室クラスに応じてグレード（Lv）を解決する疎結合口。
    // Called right before output ItemStack creation to resolve the graded ItemId by room class.
    public interface ICleanRoomGradeResolver
    {
        // このレシピがレベル分布を持つ半導体出力か（停止ゲート/差し替えの discriminator）。
        // Whether this recipe is a leveled semiconductor output (the gating discriminator).
        bool HasLevelDistribution(MachineRecipeMasterElement recipe);

        // 天井クランプの上限 ItemId（§7.1 の容量予約に使う最悪ケース＝そのクラスの最大 Lv）。
        // Ceiling ItemId for worst-case capacity reservation (the class max Lv).
        ItemId ResolveCeilingItemId(MachineRecipeMasterElement recipe, int roomClass);

        // 室クラス＋決定的 seed から最終 Lv を抽選し ItemId を返す（順序：天井→基礎分布→down-bin）。
        // Draw the final Lv ItemId from class and a deterministic seed (ceiling → base draw → down-bin).
        ItemId ResolveOutputItemId(MachineRecipeMasterElement recipe, int roomClass, long deterministicSeed);
    }
}
```

> `ItemId` の名前空間は `Core.Master`。`MachineRecipeMasterElement` は `Mooresmaster.Model.MachineRecipesModule`。両者が `Game.Block.Interface` の asmdef から参照可能か（`Core.Master`/`Mooresmaster` への参照）を確認すること。参照が無ければ asmdef に追加。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功（実装はまだ無いがインターフェースのみ）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomMachineGate.cs moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomGradeResolver.cs
git commit -m "feat(cleanroom): ICleanRoomMachineGate / ICleanRoomGradeResolver インターフェースを Game.Block.Interface に定義"
```

---

## Task 3: 停止ゲートを VanillaMachineProcessorComponent に配線（半導体機械限定）

室が Invalid のとき機械を「止めるが壊さない」（電力0と同じ）。**半導体機械にだけ**適用し、それ以外は従来通り稼働させる（既存テスト崩壊を防ぐ）。配線検証は fake gate で行い、実部屋は Task 6。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineGateTest.cs`

- [ ] **Step 1: 失敗するテストを書く（Invalid 室で半導体機械が停止／非半導体機械は停止しない／既存機械は無影響）**

`ICleanRoomMachineGate` / `ICleanRoomGradeResolver` を差し替え可能にするため、テストでは fake をDIに登録する。`MoorestechServerDIContainerOptions` のサービス差し替え手段を確認し、無ければ `ServerContext` 経由で fake を注入できるよう登録順を調整する（`creating-server-tests` スキル参照）。

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Block.Blocks.Machine;
using Mooresmaster.Model.MachineRecipesModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomMachineGateTest
    {
        // Invalid 室では半導体機械が処理を進めない（電力ありでも RemainingTicks が減らない）
        // A semiconductor machine in an Invalid room must not progress even with power supplied
        [Test]
        public void SemiconductorMachineHaltsWhenRoomInvalidTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // 露光装置レシピ（レベル分布を持つ）を引く
            // Pick the exposure recipe that carries a level distribution
            var recipe = FindLeveledRecipe();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            // fake ゲート：常に Invalid（CanOperate=false）を返すよう差し替え済みとする
            // The fake gate (registered in the container) returns CanOperate=false for this machine
            FakeCleanRoomMachineGate.SetInvalid(block.BlockInstanceId);

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            proc.SupplyPower(100000);
            var before = proc.RemainingTicks;
            GameUpdater.RunFrames(5);

            // Invalid なので開始も進捗もしない（Idle のまま）
            // Invalid → never starts processing, stays Idle
            Assert.AreEqual(ProcessState.Idle, proc.CurrentState);
        }

        // 非半導体（従来）機械は室外でも従来通り稼働する（ゲート無影響）
        // A non-semiconductor machine still runs normally regardless of room state
        [Test]
        public void NonSemiconductorMachineUnaffectedByGateTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;

            // レベル分布を持たない従来レシピ（露光装置以外）
            // A legacy recipe WITHOUT a level distribution
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            proc.SupplyPower(100000);
            GameUpdater.RunFrames(2);

            // ゲート無関係に処理開始する
            // Starts processing irrespective of any gate
            Assert.AreEqual(ProcessState.Processing, proc.CurrentState);
        }

        private static MachineRecipeMasterElement FindLeveledRecipe()
        {
            var resolver = ServerContext.GetService<ICleanRoomGradeResolver>();
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
                if (resolver.HasLevelDistribution(r)) return r;
            throw new Exception("leveled recipe not found");
        }
    }
}
```

> `FakeCleanRoomMachineGate` と DI 差し替え機構はこの Task 内で用意する（テスト用 `ICleanRoomMachineGate` 実装を Tests asmdef に置き、コンテナ生成オプションで差し替え）。差し替え手段が無ければ、`CleanRoomMachineGate` 実装（Task 5）後に「室を Invalid にして」テストする形へ統合テスト（Task 6）で代替し、本 Task は「ゲート null/非半導体は素通り」の検証に絞ってよい。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMachineGateTest"`
Expected: `SemiconductorMachineHaltsWhenRoomInvalidTest` が FAIL（ゲート未配線なので Invalid でも Processing に入る）。`NonSemiconductorMachineUnaffectedByGateTest` は PASS のはず（まだゲート無し）。

- [ ] **Step 3: 停止ゲートを Idle/Processing に配線（半導体限定・null安全）**

`VanillaMachineProcessorComponent` を編集。**discriminator＝レシピがレベル分布を持つか**で適用。ゲートは `ServerContext.GetService<ICleanRoomMachineGate>()`（null可＝テスト最小コンテナ）。

`Idle()` の開始条件に AND を追加:

```csharp
        private void Idle()
        {
            var isGetRecipe = _vanillaMachineInputInventory.TryGetRecipeElement(out var recipe);

            // 半導体（レベル分布あり）機械のみ室ゲートを適用。非対象は常に稼働可。
            // Apply the room gate only to semiconductor (leveled) machines; others always operate.
            var roomAllowsStart = !IsCleanRoomGated(recipe) || CleanRoomCanOperate();

            var isStartProcess = CurrentState == ProcessState.Idle && isGetRecipe && roomAllowsStart &&
                   _vanillaMachineInputInventory.IsAllowedToStartProcess() &&
                   _vanillaMachineOutputInventory.IsAllowedToOutputItem(recipe);

            if (isStartProcess)
            {
                CurrentState = ProcessState.Processing;
                _processingRecipe = recipe;
                _processingRecipeTicks = GameUpdater.SecondsToTicks(_processingRecipe.Time);
                _vanillaMachineInputInventory.ReduceInputSlot(_processingRecipe);
                RemainingTicks = _processingRecipeTicks;
            }
        }
```

`Processing()` 進捗を Invalid 時に止める（電力0と同じ「止まるが壊れない」）:

```csharp
        private void Processing()
        {
            // 半導体機械が処理中に室 Invalid 化したら進捗を進めない（電力0と同じ。壊さない）
            // If a semiconductor machine's room turns Invalid mid-process, freeze progress (like zero power)
            if (IsCleanRoomGated(_processingRecipe) && !CleanRoomCanOperate())
            {
                _usedPower = true; // 給電フラグは従来通り立て、進捗のみ凍結
                return;
            }

            var subTicks = MachineCurrentPowerToSubSecond.GetSubTicks(_currentPower, RequestPower);
            if (subTicks >= RemainingTicks)
            {
                RemainingTicks = 0;
                CurrentState = ProcessState.Idle;
                _vanillaMachineOutputInventory.InsertOutputSlot(_processingRecipe);
            }
            else
            {
                RemainingTicks -= subTicks;
            }

            _usedPower = true;
        }
```

discriminator とゲート問い合わせのヘルパをクラス直下に追加（クラス直下 private は `#region Internal` 禁止のため通常メソッドで並べる）:

```csharp
        // このレシピがクリーンルーム半導体出力か（停止ゲート/差し替えの単一 discriminator）
        // Whether this recipe is a clean-room semiconductor output (the single gating discriminator)
        private bool IsCleanRoomGated(MachineRecipeMasterElement recipe)
        {
            if (recipe == null) return false;
            var resolver = ServerContext.GetService<ICleanRoomGradeResolver>();
            return resolver != null && resolver.HasLevelDistribution(recipe);
        }

        // 室が稼働を許すか。ゲート未登録（最小テストコンテナ等）は true（=止めない）
        // Whether the room allows operation; a missing gate (minimal test container) defaults to true
        private bool CleanRoomCanOperate()
        {
            var gate = ServerContext.GetService<ICleanRoomMachineGate>();
            return gate == null || gate.CanOperate(_blockInstanceId);
        }
```

必要 using: `using Game.Context;`（`ServerContext`）。`_blockInstanceId` はフェーズA で導入済みフィールド（無ければフェーズA Task A3-4 のフィールドを先に追加）。

- [ ] **Step 4: コンパイル**

Run: Unity 再起動 → `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMachineGateTest"`
Expected: 両テスト PASS。

- [ ] **Step 6: 既存機械テストの回帰確認（最重要チェックポイント）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineIOTest|GearMachineIoTest|MachineModuleSlotTest"`
Expected: 全 PASS。**ここが「ゲートが広すぎないか」を捕まえる関門。** 非半導体機械が止まっていれば discriminator が誤発火しているので Step 3 を見直す。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineGateTest.cs
git commit -m "feat(cleanroom): 半導体機械限定の Invalid 停止ゲートを VanillaMachineProcessorComponent に配線"
```

---

## Task 4: CleanRoomGradeResolver（純関数：天井→分布抽選→down-bin）

クラス・基礎分布・決定的 seed から最終 Lv の ItemId を決める純関数。**部屋もDIも不要**で単体テスト可能。バランス確定書 §1 の最大グレード・down-bin率（A→Lv4/0%、B→Lv3/5%、C→Lv2/15%、D→Lv1/35%、Out→なし）で固定。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/SemiconductorLevelFamily.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomGradeResolver.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomGradeResolverTest.cs`

- [ ] **Step 1: 失敗するテストを書く（クラス天井・決定性・down-bin 標本）**

```csharp
using Core.Master;
using Game.CleanRoom.Machine;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomGradeResolverTest
    {
        // クラス A は天井 Lv4。基礎分布が Lv1 寄りでも、天井は Lv4 を許す（クランプ無し）
        // Class A ceiling is Lv4; the ceiling does not clamp anything below Lv4
        [Test]
        public void ClassACeilingIsLv4Test()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var resolver = new CleanRoomGradeResolver();
            var recipe = FindLeveledRecipe(resolver);

            var ceiling = resolver.ResolveCeilingItemId(recipe, roomClass: 0); // 0 = A
            Assert.AreEqual(ForUnitTestItemId.IcChipLv4, ceiling);
        }

        // クラス C は天井 Lv2。どの seed でも Lv2 を超えない
        // Class C ceiling is Lv2; no seed ever yields above Lv2
        [Test]
        public void ClassCNeverExceedsLv2Test()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var resolver = new CleanRoomGradeResolver();
            var recipe = FindLeveledRecipe(resolver);

            for (long seed = 0; seed < 1000; seed++)
            {
                var id = resolver.ResolveOutputItemId(recipe, roomClass: 2, deterministicSeed: seed); // 2 = C
                Assert.That(id == ForUnitTestItemId.IcChipLv1 || id == ForUnitTestItemId.IcChipLv2,
                    $"seed {seed} produced above Lv2");
            }
        }

        // 同一 seed・同一クラスは常に同一結果（決定性）
        // Same seed and class always yields the same result (determinism)
        [Test]
        public void DeterministicForSameSeedTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var resolver = new CleanRoomGradeResolver();
            var recipe = FindLeveledRecipe(resolver);

            var a = resolver.ResolveOutputItemId(recipe, roomClass: 1, deterministicSeed: 12345);
            var b = resolver.ResolveOutputItemId(recipe, roomClass: 1, deterministicSeed: 12345);
            Assert.AreEqual(a, b);
        }

        // クラス D は down-bin 率 35%：天井 Lv1 のため格下げ先が無く、常に Lv1（境界の健全性）
        // Class D down-bin 35% but ceiling is Lv1 so nothing demotes below Lv1; always Lv1
        [Test]
        public void ClassDAlwaysLv1Test()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var resolver = new CleanRoomGradeResolver();
            var recipe = FindLeveledRecipe(resolver);

            for (long seed = 0; seed < 200; seed++)
                Assert.AreEqual(ForUnitTestItemId.IcChipLv1,
                    resolver.ResolveOutputItemId(recipe, roomClass: 3, deterministicSeed: seed));
        }

        // クラス B（天井 Lv3・down-bin 5%）：大標本で down-bin が起きた回数が概ね 5% 域に収まる
        // Class B (ceiling Lv3, down-bin 5%): over a large sample, demotion frequency is near 5%
        [Test]
        public void ClassBDownBinRateApproximatelyFivePercentTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var resolver = new CleanRoomGradeResolver();
            var recipe = FindLeveledRecipe(resolver);

            // 基礎抽選結果と down-bin 後を比較して格下げ発生率を数える
            // Count demotions by comparing base draw vs post-down-bin
            int demoted = 0; int total = 10000;
            for (long seed = 0; seed < total; seed++)
            {
                var baseLv = resolver.DrawBaseLevelForTest(recipe, roomClass: 1, deterministicSeed: seed);
                var finalLv = resolver.LevelOf(resolver.ResolveOutputItemId(recipe, roomClass: 1, deterministicSeed: seed));
                if (finalLv < baseLv) demoted++;
            }
            var rate = demoted / (double)total;
            Assert.That(rate, Is.EqualTo(0.05).Within(0.015)); // 5% ± 1.5%
        }

        private static Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement FindLeveledRecipe(CleanRoomGradeResolver resolver)
        {
            foreach (var r in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
                if (resolver.HasLevelDistribution(r)) return r;
            throw new System.Exception("leveled recipe not found");
        }
    }
}
```

> `DrawBaseLevelForTest` / `LevelOf` はテスト可視のヘルパ（down-bin 前後を比較するため）。本実装で公開する。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomGradeResolverTest"`
Expected: コンパイルエラー or FAIL（`CleanRoomGradeResolver` 未実装）。

- [ ] **Step 3: SemiconductorLevelFamily を実装（ItemId↔Lv ＋ 分布参照）**

`Game.CleanRoom/Machine/SemiconductorLevelFamily.cs`:

```csharp
using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.CleanRoom.Machine
{
    // 半導体チップ ICチップ_Lv1..Lv4 の ItemId ↔ Lv 対応と、レシピの基礎レベル分布を引く。
    // ItemId<->Lv mapping for ICチップ_Lv1..Lv4 and access to a recipe's base level distribution.
    public static class SemiconductorLevelFamily
    {
        // 決定的 GUID から Lv1..Lv4 の ItemId を一度だけ解決し対応表を構築する。
        // Resolve Lv1..Lv4 ItemIds once from deterministic GUIDs and build the lookup.
        private static readonly System.Lazy<IReadOnlyList<ItemId>> LevelItemIds = new(BuildLevelItemIds);

        public const int MaxLevel = 4;

        // レシピ出力にレベル分布が定義されているか（discriminator の実体）。
        // Whether the recipe's output carries a level distribution (the discriminator core).
        public static bool HasLevelDistribution(MachineRecipeMasterElement recipe)
        {
            return TryGetDistribution(recipe, out _);
        }

        // 基礎分布（level→weight）を引く。無ければ false。
        // Get the base distribution (level -> weight); false if none.
        public static bool TryGetDistribution(MachineRecipeMasterElement recipe, out IReadOnlyList<(int level, double weight)> dist)
        {
            // machineRecipes スキーマの levelDistribution を読む（実フィールド名は生成 Module で確認）
            // Read levelDistribution from the generated recipe module (confirm the actual field name)
            dist = LevelDistributionReader.Read(recipe);
            return dist != null && dist.Count > 0;
        }

        // Lv（1..4）→ ItemId。
        // Lv (1..4) -> ItemId.
        public static ItemId ItemIdOf(int level)
        {
            return LevelItemIds.Value[level - 1];
        }

        // ItemId → Lv（1..4）。半導体チップでなければ -1。
        // ItemId -> Lv (1..4); -1 if not a semiconductor chip.
        public static int LevelOf(ItemId itemId)
        {
            var ids = LevelItemIds.Value;
            for (var i = 0; i < ids.Count; i++)
                if (ids[i] == itemId) return i + 1;
            return -1;
        }

        private static IReadOnlyList<ItemId> BuildLevelItemIds()
        {
            // 決定的 GUID（items.json / レベルファミリー生成と一致）から ItemId を引く
            // Resolve ItemIds from the deterministic GUIDs that match items.json / family generation
            var result = new List<ItemId>(MaxLevel);
            for (var lv = 1; lv <= MaxLevel; lv++)
                result.Add(MasterHolder.ItemMaster.GetItemId(SemiconductorChipGuids.For(lv)));
            return result;
        }
    }
}
```

> `LevelDistributionReader.Read` と `SemiconductorChipGuids.For` は、生成された `machineRecipes` Module の実フィールド名・実 GUID に合わせてこの Task 内で具体化する（Task 1 で確定した GUID と分布フィールド名）。フィールド名が確定したら直接読みに置き換えてよい。

- [ ] **Step 4: CleanRoomGradeResolver を実装（順序固定・salt 付き決定的乱数）**

`Game.CleanRoom/Machine/CleanRoomGradeResolver.cs`:

```csharp
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.CleanRoom.Machine
{
    // クラス→天井クランプ→基礎分布抽選→[品質シフト:予約]→down-bin→Lv確定。純粋・決定的（seed注入）。
    // Class -> ceiling clamp -> base draw -> [quality shift: reserved] -> down-bin -> Lv. Pure & deterministic.
    public class CleanRoomGradeResolver : ICleanRoomGradeResolver
    {
        // RNG サブストリーム salt（相関回避）。生産性=フェーズA側で別 salt を持つ想定。
        // RNG sub-stream salts to decorrelate; productivity uses its own salt on the phase-A side.
        private const ulong SaltLevelDraw = 0xA5A5_0000_0000_0001UL;
        private const ulong SaltDownBin = 0xA5A5_0000_0000_0002UL;

        public bool HasLevelDistribution(MachineRecipeMasterElement recipe)
        {
            return SemiconductorLevelFamily.HasLevelDistribution(recipe);
        }

        public ItemId ResolveCeilingItemId(MachineRecipeMasterElement recipe, int roomClass)
        {
            // クラスの最大 Lv（A=4,B=3,C=2,D=1,Out=0）。Out は天井 Lv0=製造不可。
            // Class max Lv (A=4,B=3,C=2,D=1,Out=0). Out => no chip producible.
            var ceiling = MaxLevelForClass(roomClass);
            return SemiconductorLevelFamily.ItemIdOf(ceiling <= 0 ? 1 : ceiling);
        }

        public ItemId ResolveOutputItemId(MachineRecipeMasterElement recipe, int roomClass, long deterministicSeed)
        {
            // 合成順序：天井 → 基礎分布抽選 → 品質シフト(予約・中立) → down-bin → Lv確定
            // Composition order: ceiling -> base draw -> [quality shift: reserved] -> down-bin -> confirm
            var ceiling = MaxLevelForClass(roomClass);
            var baseLv = DrawBaseLevel(recipe, ceiling, deterministicSeed);

            // [品質シフト穴] 将来アップグレードB がここで baseLv を上方へシフトする。本フェーズは中立。
            // [quality-shift slot] Phase-B will shift baseLv upward here; neutral for now.

            var finalLv = ApplyDownBin(baseLv, roomClass, deterministicSeed);
            return SemiconductorLevelFamily.ItemIdOf(finalLv);
        }

        #region Test helpers
        // テストが down-bin 前後を比較するための可視ヘルパ
        // Visible helpers for tests to compare pre/post down-bin
        public int DrawBaseLevelForTest(MachineRecipeMasterElement recipe, int roomClass, long deterministicSeed)
        {
            return DrawBaseLevel(recipe, MaxLevelForClass(roomClass), deterministicSeed);
        }
        public int LevelOf(ItemId itemId) => SemiconductorLevelFamily.LevelOf(itemId);
        #endregion

        // 基礎分布から天井以下の Lv を1つ抽選（重みは天井超え分を切り落として正規化）
        // Draw a Lv (<= ceiling) from the base distribution, renormalizing after truncating above-ceiling mass
        private int DrawBaseLevel(MachineRecipeMasterElement recipe, int ceiling, long seed)
        {
            SemiconductorLevelFamily.TryGetDistribution(recipe, out var dist);
            double totalWeight = 0;
            foreach (var (level, weight) in dist)
                if (level <= ceiling) totalWeight += weight;

            var roll = DeterministicRoll((ulong)seed, SaltLevelDraw) * totalWeight;
            double acc = 0;
            int chosen = 1;
            foreach (var (level, weight) in dist)
            {
                if (level > ceiling) continue;
                acc += weight;
                if (roll < acc) { chosen = level; break; }
                chosen = level;
            }
            return chosen;
        }

        // down-bin：クラスの率で1段格下げ（Lv1 は下げ先無しで据え置き）
        // Down-bin: demote one level at the class rate (Lv1 has no lower target, stays)
        private int ApplyDownBin(int level, int roomClass, long seed)
        {
            if (level <= 1) return 1;
            var rate = DownBinRateForClass(roomClass);
            return DeterministicRoll((ulong)seed, SaltDownBin) < rate ? level - 1 : level;
        }

        // クラス序数→最大 Lv（バランス確定書 §1）。本来は cleanRoomClasses マスタから引く。
        // Class ordinal -> max Lv (balance doc §1). Should be read from cleanRoomClasses master.
        private int MaxLevelForClass(int roomClass)
        {
            return roomClass switch { 0 => 4, 1 => 3, 2 => 2, 3 => 1, _ => 0 };
        }

        // クラス序数→down-bin 率（バランス確定書 §1）。本来は cleanRoomClasses マスタから引く。
        // Class ordinal -> down-bin rate (balance doc §1). Should be read from cleanRoomClasses master.
        private double DownBinRateForClass(int roomClass)
        {
            return roomClass switch { 0 => 0.0, 1 => 0.05, 2 => 0.15, 3 => 0.35, _ => 0.0 };
        }

        // salt 付き splitmix64：seed と salt から [0,1) を決定的に返す（サブストリーム decorrelate）
        // Salted splitmix64: deterministic [0,1) from seed and salt to decorrelate sub-streams
        private static double DeterministicRoll(ulong seed, ulong salt)
        {
            ulong x = seed * 0x9E3779B97F4A7C15UL + salt;
            x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27; x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return (x >> 11) * (1.0 / (1UL << 53));
        }
    }
}
```

> `MaxLevelForClass` / `DownBinRateForClass` は当面ハードコードだが、**理想は `cleanRoomClasses` マスタ（フェーズ2作成）から引く**。マスタアクセサが整っていれば置換すること。数値はバランス確定書 §1 と一致させる（変更時は両方更新）。

- [ ] **Step 5: コンパイル＋テスト**

Run: Unity 再起動 → `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomGradeResolverTest"`
Expected: 全 PASS。down-bin 率テストが帯から外れたら salt/分布/seed 経路を見直す。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/SemiconductorLevelFamily.cs moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomGradeResolver.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomGradeResolverTest.cs
git commit -m "feat(cleanroom): CleanRoomGradeResolver（天井→分布抽選→down-bin）を純関数で実装"
```

---

## Task 5: 出力差し替えと容量予約（VanillaMachineOutputInventory）＋ CleanRoomMachineGate

完了時に出力 ItemId を室クラスでグレード解決し、開始時に**天井 Lv で容量予約**（§7.1）。室引き（multi-block 占有セル）を `CleanRoomMachineGate` で実装。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomMachineGate.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: 失敗するテストを書く（クラスで出力 ItemId が変わる／非半導体は不変）**

`CleanRoomMachineGateTest.cs` に追加:

```csharp
        // クラス A 室で完了すると Lv4（天井最大）が出力スロットに入る
        // Completing in a Class A room yields Lv4 (max ceiling) in the output slot
        [Test]
        public void ClassARoomOutputsLv4Test()
        {
            // フェーズ1-3 前提：実部屋でクラスAを成立させる（基準部屋 V=75）。
            // Requires phases 1-3: build a real Class A sealed room (worked example V=75).
            // ... 露光装置を密閉室に設置 → 清浄機1台 → tick で平衡 → クラスA確認 ...
            // ... GameUpdater.RunFrames(レシピ完了まで) → OutputSlot に IcChipLv4 ...
            Assert.Ignore("requires phases 1-3 merged");
        }
```

> 実部屋テストは phases 1-3 マージ後に有効化。本 Task の必須検証は「fake gate で ResolveClass を固定し、出力 ItemId がクラスで変わる」こと（下記）。

```csharp
        // fake ゲートで ResolveClass=C を返すと、露光装置の出力が Lv2 以下になる
        // With a fake gate returning class C, the exposure output is Lv2 or below
        [Test]
        public void OutputClampedByGateClassTest()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var recipe = FindLeveledRecipe();
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, Vector3Int.one, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);

            FakeCleanRoomMachineGate.SetClass(block.BlockInstanceId, 2); // C
            FakeCleanRoomMachineGate.SetValid(block.BlockInstanceId);

            foreach (var inputItem in recipe.InputItems)
                block.GetComponent<VanillaMachineBlockInventoryComponent>().InsertItem(itemStackFactory.Create(inputItem.ItemGuid, inputItem.Count));

            var proc = block.GetComponent<VanillaMachineProcessorComponent>();
            proc.SupplyPower(100000);
            GameUpdater.RunFrames((int)proc.RemainingTicks + 5);

            var output = block.GetComponent<VanillaMachineBlockInventoryComponent>(); // 出力スロット確認
            var lv = SemiconductorLevelFamily.LevelOf(/* 出力スロットの ItemId */ output.GetItemId());
            Assert.LessOrEqual(lv, 2);
        }
```

> 出力スロット ItemId の取り出し方は `VanillaMachineOutputInventory.OutputSlot` への到達経路を確認して具体化。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OutputClampedByGateClassTest"`
Expected: FAIL（差し替え未実装＝Lv4 が出る等）。

- [ ] **Step 3: InsertOutputSlot にグレード解決を差す＋容量予約を天井 Lv に**

`VanillaMachineOutputInventory` を編集。半導体（レベル分布あり）出力のみ差し替え。`ServerContext.GetService<ICleanRoomGradeResolver>()` / `<ICleanRoomMachineGate>()`（null可）。

`InsertOutputSlot` の出力 ItemId 決定箇所:

```csharp
        public void InsertOutputSlot(MachineRecipeMasterElement machineRecipe)
        {
            var resolver = ServerContext.GetService<ICleanRoomGradeResolver>();
            var gate = ServerContext.GetService<ICleanRoomMachineGate>();
            var isLeveled = resolver != null && resolver.HasLevelDistribution(machineRecipe);
            var roomClass = (isLeveled && gate != null) ? gate.ResolveClass(_blockInstanceId) : 0;

            foreach (var itemOutput in machineRecipe.OutputItems)
                for (var i = 0; i < OutputSlot.Count; i++)
                {
                    // 半導体出力はクラス＋決定的 seed でグレード解決、それ以外はベース ItemId
                    // Semiconductor output is grade-resolved by class + deterministic seed; otherwise base
                    var outputItemId = (isLeveled && resolver != null)
                        ? resolver.ResolveOutputItemId(machineRecipe, roomClass, BuildSeed())
                        : MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);

                    var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                    if (!OutputSlot[i].IsAllowedToAdd(outputItemStack)) continue;
                    var item = OutputSlot[i].AddItem(outputItemStack).ProcessResultItemStack;
                    _itemDataStoreService.SetItem(i, item);
                    break;
                }
            // ... 液体出力は従来通り ...
        }

        // 決定的 seed（プロセッサの _processedCycleCount + _blockInstanceId 相当）を作る
        // Build the deterministic seed (mirrors processor's _processedCycleCount + _blockInstanceId)
        private long BuildSeed()
        {
            // _blockInstanceId は本インベントリも保持済み。サイクルカウントはプロセッサから受け渡す経路を確認
            // _blockInstanceId is held here; the cycle count path from the processor must be confirmed
            return ((long)_blockInstanceId.AsPrimitive() << 20) ^ _outputCycleCount;
        }
```

> **seed 経路の確定が必要。** 厳密にはフェーズA の `_processedCycleCount` を使うべきで、`InsertOutputSlot` はプロセッサから呼ばれるので、`InsertOutputSlot(MachineRecipeMasterElement, int cycleCount)` のように**サイクルカウントを引数で渡す**のが正道（デフォルト引数禁止＝呼び出し側 `Processing()` を変更）。本インベントリ内 `_outputCycleCount` で代用する場合もセーブ対象にする。Task 4 の `DeterministicRoll` と整合する `long` seed を渡すこと。

`IsAllowedToOutputItem` の容量予約を**天井 Lv の ItemId**に（§7.1。Lv1..Lv4 は別 ItemId のため、Lv3 が入ったスロットは Lv2 を受け付けない＝`IsAllowedToAdd` は `Id一致 or 空` のみ許可。確認済み）:

```csharp
        public bool IsAllowedToOutputItem(MachineRecipeMasterElement machineRecipe)
        {
            var resolver = ServerContext.GetService<ICleanRoomGradeResolver>();
            var gate = ServerContext.GetService<ICleanRoomMachineGate>();
            var isLeveled = resolver != null && resolver.HasLevelDistribution(machineRecipe);
            var roomClass = (isLeveled && gate != null) ? gate.ResolveClass(_blockInstanceId) : 0;

            foreach (var itemOutput in machineRecipe.OutputItems)
            {
                // 半導体出力は「天井 Lv の ItemId」で最悪ケース予約（§7.1）。実抽選は完了時。
                // Reserve worst-case with the ceiling-Lv ItemId for semiconductor output (§7.1)
                var outputItemId = (isLeveled && resolver != null)
                    ? resolver.ResolveCeilingItemId(machineRecipe, roomClass)
                    : MasterHolder.ItemMaster.GetItemId(itemOutput.ItemGuid);

                var outputItemStack = ServerContext.ItemStackFactory.Create(outputItemId, itemOutput.Count);
                var isAllowed = OutputSlot.Aggregate(false, (current, slot) => slot.IsAllowedToAdd(outputItemStack) || current);
                if (!isAllowed) return false;
            }
            // ... 液体チェックは従来通り ...
            return true;
        }
```

> **重要（§7.1 の罠）:** 予約は「天井 Lv」で行うが、実抽選結果が down-bin で**天井未満 Lv**になると、別 ItemId のため天井 Lv で空いていたスロットに入らない可能性がある（`IsAllowedToAdd` は Id 一致 or 空のみ）。回避策＝**完了時の格納で「空スロット」を優先**して別 Lv でも必ず入るようにする（上の `InsertOutputSlot` は空または同 Lv スロットを走査するので、開始時に1スロット空きを予約していれば down-bin 後の Lv も入る）。テスト `OutputClampedByGateClassTest` でアイテム消失が起きないことを確認すること。

必要 using: `using Game.Block.Interface.Component;`。

- [ ] **Step 4: CleanRoomMachineGate を実装（multi-block 占有セル→室）**

`Game.CleanRoom/Machine/CleanRoomMachineGate.cs`:

```csharp
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.CleanRoom.Purity; // CleanRoomPurityService / CleanRoomRoomStatus（フェーズ2配置を確認）
using Game.Context;

namespace Game.CleanRoom.Machine
{
    // 機械の全占有セルが同一の有効室に入るかで室を引き、Status とクラスを返す。
    // Resolves the room by all-occupied-cells-in-one-valid-room, returning status and class.
    public class CleanRoomMachineGate : ICleanRoomMachineGate
    {
        private readonly CleanRoomDetectionSystem _detection;
        private readonly CleanRoomPurityService _purity;

        public CleanRoomMachineGate(CleanRoomDetectionSystem detection, CleanRoomPurityService purity)
        {
            _detection = detection;
            _purity = purity;
        }

        public bool CanOperate(BlockInstanceId machine)
        {
            // 室が引けない（外置き/またがり）または Invalid なら false。Degraded/猶予中は true。
            // False if no single valid room (outside/straddle) or Invalid; Degraded/grace is true.
            if (!TryGetValidRoom(machine, out var state)) return false;
            return state.Status != CleanRoomRoomStatus.Invalid;
        }

        public int ResolveClass(BlockInstanceId machine)
        {
            // 室が引けなければ最悪クラス（Out=4 相当）。引ければクラス序数を返す。
            // Worst class if no room; otherwise the class ordinal.
            if (!TryGetValidRoom(machine, out var state)) return 4; // Out
            return (int)state.CurrentClass;
        }

        // 機械の MinPos..MaxPos 全占有セルが同一の有効室に含まれるかを判定し、純度状態を返す。
        // Check all occupied cells (MinPos..MaxPos) fall in ONE valid room; return its purity state.
        private bool TryGetValidRoom(BlockInstanceId machine, out CleanRoomPurityState state)
        {
            state = null;
            var block = ServerContext.WorldBlockDatastore.GetBlock(machine); // 取得経路は確認
            var pos = block.BlockPositionInfo;

            CleanRoom room = null;
            for (var x = pos.MinPos.x; x <= pos.MaxPos.x; x++)
            for (var y = pos.MinPos.y; y <= pos.MaxPos.y; y++)
            for (var z = pos.MinPos.z; z <= pos.MaxPos.z; z++)
            {
                if (!_detection.TryGetRoomAt(new UnityEngine.Vector3Int(x, y, z), out var r)) return false;
                if (room == null) room = r;
                else if (r.Id != room.Id) return false; // またがり
            }
            if (room == null) return false;
            return _purity.TryGetState(room, out state) && state.Status != CleanRoomRoomStatus.Invalid
                ? (state != null)
                : _purity.TryGetState(room, out state); // 状態取得（Invalid でも state は返し CanOperate 側で判定）
        }
    }
}
```

> `TryGetRoomAt` / `TryGetRoomContainingBlock` / `GetBlock(BlockInstanceId)` / `CleanRoomRoomStatus`/`CleanRoomClass` の名前空間・シグネチャはフェーズ1/2 実装を確認して合わせる。`TryGetValidRoom` の末尾は「state を必ず取得し、Invalid 判定は呼び出し側に委ねる」よう簡潔化すること（上の三項は冗長なので実装時に整理）。占有セルが機械本体を含む＝室の `Cells`（空セル）には機械セルは無いため、**機械の占有セルではなく「機械に隣接する室」を引く**のが正しい場合がある。フェーズ2 §1.4 の「占有セルが室 Cells に入るか」の規約に合わせること（codemap §1.4 / §4 の `TryGetRoomContainingBlock` を使用）。

- [ ] **Step 5: DI 登録（Server.Boot）**

`MoorestechServerDIContainerGenerator.cs` に登録。`Game.CleanRoom` への asmdef 参照を追加（既にフェーズ2で追加済みなら不要）:

```csharp
            // クリーンルーム製造機ゲート／グレード解決を登録（機械は ServerContext.GetService で引く）
            // Register the clean-room machine gate / grade resolver (machines fetch via ServerContext.GetService)
            services.AddSingleton<ICleanRoomMachineGate, CleanRoomMachineGate>();
            services.AddSingleton<ICleanRoomGradeResolver, CleanRoomGradeResolver>();
```

> 登録方式（`AddSingleton` か既存の `services.AddSingleton(...)` 記法か）は既存ファイルの流儀に合わせる。eager 化は不要（機械が必要時に引く）。

- [ ] **Step 6: コンパイル＋テスト**

Run: Unity 再起動 → `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomMachineGateTest"`
Expected: `OutputClampedByGateClassTest` PASS。アイテム消失が無いこと。

- [ ] **Step 7: 回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineIOTest|GearMachineIoTest"`
Expected: 全 PASS（非半導体機械の出力は不変）。

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs moorestech_server/Assets/Scripts/Game.CleanRoom/Machine/CleanRoomMachineGate.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineGateTest.cs
git commit -m "feat(cleanroom): 出力グレード差し替え＋天井容量予約と CleanRoomMachineGate（占有セル→室）を実装"
```

---

## Task 6: 実部屋の統合テスト（phases 1-3 マージ後に有効化）

実際の密閉室でクラスを成立させ、出力 Lv・Invalid 停止・またがり停止を end-to-end で検証する。**phases 1-3 がマージされて初めて成立**するため、未マージ環境では `Assert.Ignore` で枠だけ置く。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineGateTest.cs`

- [ ] **Step 1: 統合テストを書く（4ケース）**

```csharp
        // クラス A 室（基準部屋 V=75・清浄機1台で平衡 C=3.2）→ 露光装置は Lv4 を出す
        // Class A room (worked example) -> exposure machine outputs Lv4
        [Test]
        public void RealRoomClassAOutputsLv4Test()
        {
            // 1. 壁で 5×5×3 の密閉室を作り露光装置＋清浄機を設置
            // 2. tick で平衡 → CleanRoomPurityService がクラス A を確定（バランス §4）
            // 3. レシピ完了まで tick → OutputSlot に IcChipLv4
            Assert.Ignore("enable after phases 1-3 merged");
        }

        // クラス C 室 → 出力は Lv2 以下。決定的 seed で down-bin 標本も固定
        // Class C room -> output <= Lv2; deterministic down-bin sample
        [Test]
        public void RealRoomClassCNeverExceedsLv2Test()
        {
            Assert.Ignore("enable after phases 1-3 merged");
        }

        // 室を Invalid 化（密閉破壊→猶予切れ）→ 露光装置が停止（RemainingTicks 据え置き）
        // Room turned Invalid (seal broken past grace) -> machine halts (RemainingTicks frozen)
        [Test]
        public void RealRoomInvalidHaltsMachineTest()
        {
            Assert.Ignore("enable after phases 1-3 merged");
        }

        // 室境界をまたぐ multi-block 機械（占有セルが複数室/室外）→ 室外扱いで停止
        // Multi-block machine straddling the boundary -> treated as outside -> halts
        [Test]
        public void StraddlingMachineTreatedAsOutsideHaltsTest()
        {
            Assert.Ignore("enable after phases 1-3 merged");
        }
```

- [ ] **Step 2: phases 1-3 がマージ済みなら `Assert.Ignore` を外して実装**

各ケースを実部屋構築で具体化（バランス確定書 §4 の worked example でクラス成立を固定）。Invalid 化は密閉壁を1個破壊し猶予 5 秒（100 tick）超 tick で発火。またがりは 1×1×2 等の機械を室壁にまたがせて設置。

- [ ] **Step 3: テスト**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全 PASS（または phases 1-3 未マージ時は Ignore 4件＋実装済みテスト PASS）。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomMachineGateTest.cs
git commit -m "test(cleanroom): 実部屋クラス別出力・Invalid停止・またがり停止の統合テスト"
```

---

## Self-Review（実装完了前チェック）

### コードマップ §4 カバレッジ
- [ ] (a) Invalid 停止 → Task 3（半導体限定・Idle/Processing 両方・Degraded/猶予中は稼働）。コードマップ §4「電力0と同じ止まるが壊れない」を満たす。
- [ ] (b) 最大グレード天井 → Task 4 `ResolveCeilingItemId`/`DrawBaseLevel`（天井超え分を切り落とし）。
- [ ] (c) down-bin 格下げ → Task 4 `ApplyDownBin`（クラス率）。
- [ ] 製造機フック2点（`VanillaMachineProcessorComponent` 停止ゲート ＋ `VanillaMachineOutputInventory.InsertOutputSlot` 差し替え）→ Task 3/5。
- [ ] `ICleanRoomMachineGate` 経由の疎結合（`Game.Block` が `Game.CleanRoom` を直接参照しない）→ Task 2＋`ServerContext.GetService`。
- [ ] multi-block 占有＝全占有セルが同一有効室、またがり＝室外停止 → Task 5 `CleanRoomMachineGate.TryGetValidRoom` ＋ Task 6 統合テスト。

### 決定メモ §5 引き渡し事項カバレッジ
- [ ] 1. レベルファミリー＝半導体限定・決定的 GUID → Task 1。
- [ ] 2. レベル決定＝§7.2 の③・単一の決定的乱数源（フェーズA流儀） → Task 4 salt 付き `DeterministicRoll`。
- [ ] 3. 合成順序固定（室Invalid停止 → 天井 → 基礎分布 → down-bin → Lv確定） → Task 3＋4。
- [ ] 4. `InsertOutputSlot` 直前に③／multi-block は `MinPos..MaxPos`×室引き → Task 5。
- [ ] 5. アップグレードB の品質シフト穴を温存（埋めない・壊さない） → Task 4 `ResolveOutputItemId` 内の [品質シフト穴] コメント位置。

### 設計書 §4/§8 整合
- [ ] §4 クラス効果（天井＋down-bin）＝バランス §1（A→Lv4/0%, B→Lv3/5%, C→Lv2/15%, D→Lv1/35%, Out→なし）でテスト固定（Task 4）。
- [ ] §8 機械関係（Valid 通常／Degraded＋猶予は稼働継続／Invalid 停止）＝ `CanOperate` が Invalid のみ false（Task 5 ゲート＋Task 3 配線）。

### プレースホルダ走査
- [ ] `TODO`/`FIXME`/未実装スタブが残っていないか全変更ファイルを grep。
- [ ] `LevelDistributionReader.Read` / `SemiconductorChipGuids.For` / `MaxLevelForClass`/`DownBinRateForClass` は実マスタ参照に置換済みか（ハードコードのままなら数値がバランス §1 と一致し、かつコメントで明示）。
- [ ] `BuildSeed` のサイクルカウント経路が確定し、フェーズA `_processedCycleCount` と一本化されているか（テストの決定性が seed に依存）。

### 型整合（契約名）
- [ ] `ICleanRoomMachineGate` / `CleanRoomMachineGate` / `CleanRoomGradeResolver` / `CleanRoomClass` / `CleanRoomRoomStatus` / `CleanRoomPurityService` を**契約どおりの名前**で使用（コードマップ §1／決定メモ）。
- [ ] `CleanRoomPurityState` のアクセサ（`Status`/`CurrentClass`）がフェーズ2 の実装と一致。

### ③ レベル決定 API サーフェス（決定メモ §6：アップグレードB への公開仕様）
将来のアップグレード フェーズB が品質シフトを差すために、フェーズ4 が確定し公開する API を明文化する:

- **分布テーブル型:** `IReadOnlyList<(int level, double weight)>`（`machineRecipes` マスタの `levelDistribution` フィールド＝`{level:int, weight:double}` の配列から `SemiconductorLevelFamily.TryGetDistribution(recipe, out dist)` で取得）。weight は非正規化（合計1でなくてよい・抽選時に正規化）。
- **抽選関数シグネチャ:** `ItemId ICleanRoomGradeResolver.ResolveOutputItemId(MachineRecipeMasterElement recipe, int roomClass, long deterministicSeed)`。容量予約用に `ItemId ResolveCeilingItemId(MachineRecipeMasterElement, int roomClass)`。discriminator は `bool HasLevelDistribution(MachineRecipeMasterElement)`。
- **合成順序（固定）:** 天井クランプ → 基礎分布抽選 → **[品質シフト挿入点：アップグレードB]** → down-bin → Lv確定。アップグレードB は基礎分布抽選と down-bin の**間**に確率シフトを挿す（`ResolveOutputItemId` 内の `[品質シフト穴]` コメント位置）。
- **乱数規約:** 単一の決定的 seed（`_processedCycleCount` ＋ `_blockInstanceId` 由来 `long`）を、**サブストリームごとに salt** して splitmix64 で [0,1) 化する。salt 割り当て＝生産性（フェーズA）／level-draw=`0xA5A5…0001`／down-bin=`0xA5A5…0002`。アップグレードB の品質シフトが乱数を引くなら**新たな salt 定数**を割り当てる（既存 salt と衝突させない）。

---

## 変更ファイル総覧（フェーズ4）

| 区分 | ファイル | 種別 |
|---|---|---|
| マスタ | `VanillaSchema/items.yml`（ICチップ レベルファミリー） | 改 |
| マスタ | `VanillaSchema/machineRecipes.yml`（levelDistribution） | 改 |
| マスタ | `Core.Master/_CompileRequester.cs` | 改 |
| マスタ | テスト mod `items.json`/`machineRecipes.json`/`blocks.json`／`ForUnitTestItemId.cs` | 改 |
| インターフェース | `Game.Block.Interface/Component/ICleanRoomMachineGate.cs` / `ICleanRoomGradeResolver.cs` | 新規 |
| 製造機 | `Game.Block/Blocks/Machine/VanillaMachineProcessorComponent.cs` | 改 |
| 製造機 | `Game.Block/Blocks/Machine/Inventory/VanillaMachineOutputInventory.cs` | 改 |
| クリーンルーム | `Game.CleanRoom/Machine/CleanRoomMachineGate.cs` / `CleanRoomGradeResolver.cs` / `SemiconductorLevelFamily.cs` | 新規 |
| DI | `Server.Boot/MoorestechServerDIContainerGenerator.cs` | 改 |
| テスト | `Tests/CombinedTest/Core/CleanRoomGradeResolverTest.cs` / `CleanRoomMachineGateTest.cs` | 新規 |
