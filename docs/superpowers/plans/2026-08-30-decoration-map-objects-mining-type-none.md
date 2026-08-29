# 装飾mapObject（miningType: None）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `miningType: None` の装飾mapObjectを導入し、ブッシュ系13件とメサ崖・地層系26件を「狙えない・落とさない・ピンされない」完全な装飾物にする。

**Architecture:** スキーマ `VanillaSchema/map.yml` の `miningType` に第3値 `None` を足す（SourceGeneratorが `MapObjectMasterElement.MiningTypeConst.None` と `NoneMiningParam` を生成する）。サーバーは `MapObjectMasterUtil` で「Noneはearn無し／None以外はearn必須」を検証し、`MapObjectMiningService` が `None` への攻撃を `NotInteractable` で拒否する。クライアントは `MapObjectGameObject.Initialize` で `None` 個体のレイターゲットコライダーを無効化し、レティクルに乗せない（HPバーはフォーカス時のみ表示なので自動的に出ない）。マスタデータ39件の書き換えは `moorestech_master` の別PRで行い、本repoのピンを更新する。

**Tech Stack:** Unity 2022 / C# (NUnit) / mooresmaster SourceGenerator / Python 3（マスタJSON一括編集） / uloop CLI

## Requirements

設計対話（2026-08-30）で確定した要件。全行が `docs/adr/0043-non-interactive-decoration-map-objects.md` に対応する。

- R1: `VanillaSchema/map.yml` の `miningType` が `PickUp | Mining | None` の3値になり、`miningParam` switch に `None → optional 空object` の case がある。受け入れ基準: コンパイル後に `MapObjectMasterElement.MiningTypeConst.None` と `NoneMiningParam` が参照できる。
- R2: `MapObjectMasterUtil.Validate` が「`None` なのに `earnItems` が非空」をエラーとして報告する。受け入れ基準: 該当要素を含むmapで `Validate` が false を返し、ログに `None must have empty EarnItems` が含まれる。
- R3: 既存の「`earnItems` 空はエラー」検査は `None` 以外に限定される。受け入れ基準: `None` かつ `earnItems: []` の要素だけを含むmapで `Validate` が true を返す。
- R4: サーバー `MapObjectMiningService.TryAttack` は `None` の対象へ `MiningAttackResult.NotInteractable` を返し、HPを減らさず取得物も生成しない。受け入れ基準: 自動テストで `NotInteractable` が返り `IsDestroyed == false` のまま。
- R5: `MiningProtocol` は `NotInteractable` を拒否ログで畳み、例外を投げない。受け入れ基準: switch に case が追加され `ArgumentOutOfRangeException` に落ちない。
- R6: クライアント `MapObjectGameObject.Initialize` は `None` 個体の全 `MapObjectRayTarget` のコライダーを無効化する。受け入れ基準: 自動テストで `Collider.enabled == false`、`Mining` 個体では `true` のまま。
- R7: クライアント `MapObjectGameObject.TryBeginHandMining` は `None` 個体で `MiningStartOutcome.Unavailable` を返す（`MiningMiningParam` へのキャストに到達しない）。受け入れ基準: 自動テストで `Unavailable`。
- R8: `moorestech_master` の map.json でブッシュ系13件＋メサ崖・地層系26件（計39件、GUIDは Task 6 に列挙）が `miningType: "None"`・`miningParam: {}`・`earnItems: []` になる。それ以外の156件は差分ゼロ。受け入れ基準: Pythonアサーションが通る。
- R9: 本repoの `.moorestech-external-revisions.json` の `moorestech_master` ピンが、master側PRブランチのpush済みコミットを指す。受け入れ基準: そのハッシュが `git ls-remote origin` に存在する。
- R10: 39件は物理コライダー（歩行の当たり）を据え置く。受け入れ基準: `DestroyMapObject` 以外で `MapObjectRayTarget` 以外のColliderを触らない。

**やらないこと（スコープ境界）:**
- generation.json（配置）の変更。39件の配置は今のまま残す
- Prefab／アドレサブルの変更（レイターゲットはprefabに残し、実行時に無効化する）
- ピン解決ロジック（`ChallengeMaster.TryResolvePinTargets`）の変更。`earnItems: []` により索引から自然に外れる
- Web UI・ツールチップ文言の変更（狙えないので表示経路に到達しない）
- 他の草花（サボテン・DryGrass・Peanut・Wildflowers・Opuntia）や Boulders・Rubble の扱い変更

## Global Constraints

- **partial 禁止・`Func<>` 禁止・try-catch 原則禁止・デフォルト引数禁止**（AGENTS.md）。
- **コメントは日本語・英語の2行セット**（`// 日本語` → `// English`）、各1行。日本語目安は処理20字・メソッド30字。
- **`#region Internal` はメソッド内ローカル関数限定。** クラス直下のprivateメソッドを囲わない。
- **.cs を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。**
- **スキーマ変更時は `_CompileRequester.cs` の `dummyText` を変えて同時にコミットする**（edit-schema スキル）。`csc.rsp` は map.yml 登録済みなので変更不要。
- **`.meta` は手動作成しない。** 新規.csファイルはUnityが.metaを生成するので、生成後にその.metaもコミットする。
- **テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。** ドメインリロード中エラーは45秒待って再試行。
- **マスタJSONの整形:** `moorestech_master` の map.json は `json.dumps(d, ensure_ascii=False, indent=2)` がバイト一致（2026-08-28 plan で検証済み）。テスト用 `forUnitTest/master/map.json` は4スペースインデントで、Task 2 では手編集で1要素を追記する。
- **`.moorestech-external-revisions.json` はUnityが書き戻すことがある。** コミット時は `git add -A` を使わず当該ファイルを明示指定し、直前に `git diff` で中身を確認する。
- **別repo（moorestech_master）の変更もpushしてPRを作る（必須）。** ブランチ名は `feature/decoration-map-objects-none`。master側は worktree を切らず `origin/master` から `git switch -c` する（現在 detached HEAD なので注意）。
- **worktree運用:** 本planは `moores-wt new feature/decoration-map-objects-none` で切った使い捨てworktreeで実行し、PR後に `moores-wt rm` で畳む。

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `VanillaSchema/map.yml` | Modify | `miningType` に `None` を追加、`miningParam` switch に `None` case を追加 |
| `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` | Modify | SourceGenerator 再実行の印を更新 |
| `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json` | Modify | `None` のテスト用mapObject `vanilla:TestDecoration` を1件追加（配置は追加しない） |
| `moorestech_server/Assets/Scripts/Core.Master/Validator/MapObjectMasterUtil.cs` | Modify | `EarnItemsValidation` を `None` 対応に書き換え |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapObjectMasterValidationTest.cs` | Modify | R2・R3 のテスト2本を追加 |
| `moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs` | Modify | `MiningAttackResult.NotInteractable` を追加し `None` を拒否 |
| `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs` | Modify | switch に `NotInteractable` case を追加 |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MapObjectNotInteractableMiningTest.cs` | Create | R4 のテスト |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectRayTarget.cs` | Modify | `SetInteractable(bool)` でコライダーを切り替える |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs` | Modify | `None` 個体のレイ除外と `TryBeginHandMining` のガード |
| `moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectDecorationRayTargetTest.cs` | Create | R6・R7 のテスト |
| `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` | Modify（別repo） | 39件を `None` へ |
| `.moorestech-external-revisions.json` | Modify | master ピン更新 |

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `miningType: None` + 空case | `VanillaSchema/map.yml` | switch/cases の optional 空object | 同ファイル `mapVeins.handMiningType: none` の `when: none / optional: true / properties: []` |
| 2 | None検証 | `Core.Master.Validator.MapObjectMasterUtil.Validate` 内ローカル関数 | `logs` 文字列を積む | 同ファイル `EarnItemsValidation()`（2026-08-28追加） |
| 3 | `MiningAttackResult.NotInteractable` | `Game.Map.MapObjectMiningService` | enum + 早期return | 同ファイル `AlreadyDestroyed`（2026-08 追加の破壊済みガード） |
| 4 | プロトコルの拒否畳み | `Server.Protocol.PacketResponse.MiningProtocol.MineMapObject` | switch case 追加 | 同switchの `InventoryFull` |
| 5 | レイ除外 | `Client.Game.InGame.Map.MapObject.MapObjectRayTarget.SetInteractable` / `MapObjectGameObject.Initialize` | Collider.enabled 切替 | `MapObjectGameObject.DestroyMapObject`（破壊時に全Colliderを無効化する同型） |
| 6 | クライアントガード | `MapObjectGameObject.IsAvailable` | 既存の可用判定へ条件追加 | 同プロパティの `MapObjectMasterElement != null` |

- データフロー: 採掘は「レイ→`IMiningRayTarget`→`TryBeginHandMining`→`SendAttack`→サーバー`TryAttack`」の既存一方向連鎖。本変更は入口（レイ）と終点（サーバー）にゲートを足すだけで、書き手・読み手は増えない。
- 新規パターン: なし。機構選択の分岐点（既存機構の抑止・迂回・並行複製）なし。
- 機能パリティ死活表: 「ブッシュ／崖を殴って原木・石を得る」操作は消える（ユーザー裁定 2026-08-30「A 完全な装飾物」で受容済み）。歩行の衝突・遠景表示・最寄り探索（`IsSearchable`）は不変。

---

### Task 1: スキーマに `None` を追加する

**Files:**
- Modify: `VanillaSchema/map.yml:44-58`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs:9`

**Interfaces:**
- Consumes: なし
- Produces: 生成型 `MapObjectMasterElement.MiningTypeConst.None`（string定数）、`Mooresmaster.Model.MapModule.NoneMiningParam`（空クラス、`MiningParam` のswitch結果型）。既存生成の `NoneHandMiningParam` / `PickUpMiningParam` と同じ命名規則

- [x] **Step 1: map.yml を編集する**

`- key: miningType` の `options` に `None` を、`miningParam` の `cases` に `None` を足す。

```yaml
    - key: miningType
      type: enum
      options:
      - PickUp
      - Mining
      - None
    - key: miningParam
      switch: ./miningType
      cases:
      - when: PickUp
        type: object
        optional: true
        properties: []
      # 装飾物。狙えず落とさず、earnItemsは空を強制する
      # Decoration: cannot be aimed at or drop anything; earnItems is forced empty
      - when: None
        type: object
        optional: true
        properties: []
      - when: Mining
        type: object
        properties:
        - key: miningTools
```

- [x] **Step 2: `_CompileRequester.cs` の印を更新する**

```csharp
    private const string dummyText = "2026-08-30-map-mining-type-none";
```

- [x] **Step 3: コンパイルして生成型を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。続けて `strings moorestech_client/Library/ScriptAssemblies/Core.Master.dll | grep -E "^NoneMiningParam$"` が `NoneMiningParam` を出力する。

- [x] **Step 4: コミットする**

```bash
git add VanillaSchema/map.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat: mapObjectのminingTypeにNone（装飾物）を追加する"
```

---

### Task 2: テスト用マスタに `None` の mapObject を追加する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json`

**Interfaces:**
- Consumes: Task 1 の `None`
- Produces: テスト用GUID `00000000-0000-4444-0000-000000000001`（`vanilla:TestDecoration`）。Task 3・4・5 のテストがこのGUIDを使う。**配置（`ForUnitTest/map/map.json`）には追加しない**（`GetMapDataProtocolTest` が配置6件を固定している）

- [x] **Step 1: mapObjects 配列の末尾（`vanilla:TestRubbleRock` の後）に1要素を追記する**

既存要素と同じ4スペースインデントで、配列の閉じ `]` の直前に追加する。

```json
        {
            "earnItemHpInterval": 10,
            "hp": 10,
            "mapObjectName": "vanilla:TestDecoration",
            "addressablePath": "Vanilla/Environment/Tree",
            "soundEffectType": "tree",
            "miningType": "None",
            "earnItems": [],
            "mapObjectGuid": "00000000-0000-4444-0000-000000000001",
            "miningParam": {},
            "#memo": "装飾物。狙えず落とさない。配置(map/map.json)には置かない(GetMapDataProtocolTestが6件固定)",
            "distanceVisibilityType": "cullable"
        }
```

- [x] **Step 2: JSONとして壊れていないことを確認する**

Run: `python3 -c "import json;d=json.load(open('moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json'));print(len(d['mapObjects']), d['mapObjects'][-1]['miningType'])"`
Expected: `5 None`

- [x] **Step 3: 既存のマスタ関連テストがまだ通る（この時点では earnItems 空検査が None にも効くので失敗するはず）ことを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectMasterValidationTest|MapObjectPinTargetResolutionTest"`
Expected: 両方とも `SetUp` のDI起動で `MasterHolder.InitializeMaster` が `Master data validation failed: ... has empty EarnItems` を投げて FAIL（Task 3 で直す）。この失敗が出ないなら `MasterHolder.cs:109` の検証が走っていないので原因を調べる。

- [x] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json
git commit -m "test: テスト用マスタにminingType Noneの装飾mapObjectを追加する"
```

---

### Task 3: マスタ検証を `None` 対応にする

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/MapObjectMasterUtil.cs:52-66`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapObjectMasterValidationTest.cs`

**Interfaces:**
- Consumes: Task 1 の `MapObjectMasterElement.MiningTypeConst.None`、Task 2 の `vanilla:TestDecoration`
- Produces: ログ文言 `None must have empty EarnItems`（R2）。既存文言 `has empty EarnItems` は非Noneに限定（R3）

- [x] **Step 1: 失敗するテスト2本を `MapObjectMasterValidationTest` に追加する**

```csharp
        [Test]
        public void Noneのmapobjectがearnitemsを持つと失敗する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var decoration = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "None");
            var miningMapObject = ((JArray)json["mapObjects"]).Children<JObject>()
                .Single(element => (string)element["miningType"] == "Mining");

            // 実在するearnItemを装飾物へ複製し、foreignKey成功と矛盾失敗を分離する
            // Copy a valid earn item onto the decoration so foreign-key success is isolated from the contradiction failure
            decoration["earnItems"] = miningMapObject["earnItems"].DeepClone();
            var master = new MapObjectMaster(json);

            Assert.IsFalse(master.Validate(out var logs));
            StringAssert.Contains("None must have empty EarnItems", logs);
        }

        [Test]
        public void Noneのmapobjectはearnitemsが空でも成功する()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory,
                "mods", "forUnitTest", "master", "map.json");
            var json = JObject.Parse(File.ReadAllText(path));
            var master = new MapObjectMaster(json);

            // テストマスタはNoneの装飾物を1件含んだまま検証を通る
            // The test master passes validation while holding one None decoration
            Assert.IsTrue(master.Validate(out var logs), logs);
        }
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectMasterValidationTest"`
Expected: `Noneのmapobjectがearnitemsを持つと失敗する` は文言不一致で FAIL、`Noneのmapobjectはearnitemsが空でも成功する` は `has empty EarnItems` で FAIL。

- [x] **Step 3: `EarnItemsValidation` を書き換える**

`MapObjectMasterUtil.cs` の `EarnItemsValidation()` ローカル関数を差し替える。

```csharp
            string EarnItemsValidation()
            {
                // 装飾物(None)はドロップを持たず、それ以外は殴っても空振りしないよう必須
                // A decoration (None) must not drop anything; everything else must, or mining it would be a whiff
                var logs = "";
                foreach (var mapObjectElement in map.MapObjects)
                {
                    var isDecoration = mapObjectElement.MiningType == MapObjectMasterElement.MiningTypeConst.None;
                    var hasEarnItems = mapObjectElement.EarnItems.Length != 0;
                    if (isDecoration && hasEarnItems)
                    {
                        logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} miningType None must have empty EarnItems\n";
                    }
                    if (!isDecoration && !hasEarnItems)
                    {
                        logs += $"[MapObjectMaster] Name:{mapObjectElement.MapObjectName} has empty EarnItems\n";
                    }
                }

                return logs;
            }
```

- [x] **Step 4: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectMasterValidationTest|MapObjectPinTargetResolutionTest|ChallengeMasterValidationTest"`
Expected: 全件 PASS（既存3本＋新規2本、ピン解決・チャレンジ検証も無傷）。

- [x] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Core.Master/Validator/MapObjectMasterUtil.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapObjectMasterValidationTest.cs
git commit -m "feat: マスタ検証でminingType NoneのearnItems空を強制する"
```

---

### Task 4: サーバーが `None` への攻撃を拒否する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs:12-24,40-50`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs:104-117`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MapObjectNotInteractableMiningTest.cs`

**Interfaces:**
- Consumes: Task 2 のGUID `00000000-0000-4444-0000-000000000001`、`IMapObjectFactory.Create(int instanceId, Guid mapObjectGuid, int currentHp, bool isDestroyed, Vector3 position)`
- Produces: `MiningAttackResult.NotInteractable`

- [x] **Step 1: 失敗するテストを書く**

```csharp
using System;
using Core.Master;
using Game.Map;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    ///     装飾物(miningType None)への攻撃がサーバーで拒否されることを検証する
    ///     Verifies the server rejects an attack on a decoration (miningType None)
    /// </summary>
    public class MapObjectNotInteractableMiningTest
    {
        private const int PlayerId = 0;

        // テストマスタの装飾物。配置には無いのでファクトリで生成する
        // The decoration in the test master; it has no placement, so the factory creates it
        private static readonly Guid DecorationMapObjectGuid = Guid.Parse("00000000-0000-4444-0000-000000000001");

        [Test]
        public void 装飾物への攻撃はNotInteractableで拒否されHPも減らない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var miningService = serviceProvider.GetService<MapObjectMiningService>();
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var decoration = serviceProvider.GetService<IMapObjectFactory>().Create(100, DecorationMapObjectGuid, 10, false, Vector3.zero);

            // 石の斧を装備していても装飾物は削れない
            // Even with a stone axe equipped, a decoration cannot be worn down
            var axeId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var equippedItem = ServerContext.ItemStackFactory.Create(axeId, 1);
            var result = miningService.TryAttack(PlayerId, decoration, equippedItem, playerInventory.MainOpenableInventory, out var earnedItems);

            Assert.AreEqual(MiningAttackResult.NotInteractable, result);
            Assert.IsNull(earnedItems);
            Assert.IsFalse(decoration.IsDestroyed);
            Assert.AreEqual(10, decoration.CurrentHp);
        }
    }
}
```

`using Game.Context;` を先頭のusingに加える（`ServerContext`）。`00000000-0000-0000-1234-000000000001` は `vanilla:TestMiningRock` の `miningTools[0].toolItemGuid` で、テストマスタに実在する。`IMapObject.CurrentHp` は `Game.Map.Interface/MapObject/IMapObject.cs:37` に存在する。

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MiningAttackResult` に `NotInteractable` が無い旨のコンパイルエラー。

- [x] **Step 3: `MapObjectMiningService` に `NotInteractable` を追加して拒否する**

enum:

```csharp
        // 取得物を受け取れないため採掘を成立させない
        // Mining is refused because the drops could not be received
        InventoryFull,

        // 装飾物(miningType None)は攻撃対象でない。クライアント側のレイ除外と二重の防御
        // A decoration (miningType None) is not attackable; second line of defense behind the client's ray exclusion
        NotInteractable,
```

`TryAttack` の `AlreadyDestroyed` チェック直後、`CanReceiveEarnItems` の前に挿入する:

```csharp
            // 装飾物は偽造要求でも削れない
            // A decoration cannot be worn down even by a forged request
            var mapObjectElement = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);
            if (mapObjectElement.MiningType == MapObjectMasterElement.MiningTypeConst.None) return MiningAttackResult.NotInteractable;

            // 受け取れない取得物は消滅するので、対象を削る前に空きを確かめる
            // Undeliverable drops would vanish, so verify the free space before wearing the target down
            if (!CanReceiveEarnItems(mapObjectElement)) return MiningAttackResult.InventoryFull;
```

（既存の `var mapObjectElement = ...` 行は上に移動するので重複させない）

- [x] **Step 4: `MiningProtocol.MineMapObject` の switch に case を足す**

```csharp
                    case MiningAttackResult.InventoryFull:
                    case MiningAttackResult.NotInteractable:
                        Debug.Log($"Mining attack rejected. playerId:{data.PlayerId} instanceId:{data.InstanceId} result:{result}");
                        return null;
```

- [x] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectNotInteractableMiningTest|MapObjectMiningDestroyGuardTest|MapObjectAcquisitionProtocolTest"`
Expected: 全件 PASS。

- [x] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs "moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MapObjectNotInteractableMiningTest.cs" "moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/MapObjectNotInteractableMiningTest.cs.meta"
git commit -m "feat: サーバーがminingType Noneの装飾物への攻撃をNotInteractableで拒否する"
```

---

### Task 5: クライアントが `None` 個体をレイから除外する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectRayTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs:48-52,95-101`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectDecorationRayTargetTest.cs`

**Interfaces:**
- Consumes: Task 2 のGUID `00000000-0000-4444-0000-000000000001`（None）と既存 `00000000-0000-2222-0000-000000000001`（Mining）
- Produces: `MapObjectRayTarget.SetInteractable(bool interactable)`

- [x] **Step 1: 失敗するテストを書く**

```csharp
using System;
using Client.Game.InGame.Map.MapObject;
using Client.Game.InGame.Mining;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     装飾物(miningType None)がレイターゲットから外れ採掘を始められないことを検証する
    ///     Verifies a decoration (miningType None) drops out of the ray target and can never start mining
    /// </summary>
    public class MapObjectDecorationRayTargetTest
    {
        private static readonly Guid DecorationGuid = new("00000000-0000-4444-0000-000000000001");
        private static readonly Guid MiningRockGuid = new("00000000-0000-2222-0000-000000000001");

        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void 装飾物はレイターゲットのコライダーが無効化され採掘開始もUnavailableになる()
        {
            var (mapObject, rayCollider) = Build(DecorationGuid);

            Assert.IsFalse(rayCollider.enabled);
            Assert.IsFalse(mapObject.IsAvailable);
            Assert.AreEqual(MiningStartOutcome.Unavailable, mapObject.TryBeginHandMining(ItemMaster.EmptyItemId, out _, out _));
        }

        [Test]
        public void 採掘可能なmapObjectはレイターゲットのコライダーが有効のまま()
        {
            var (mapObject, rayCollider) = Build(MiningRockGuid);

            Assert.IsTrue(rayCollider.enabled);
            Assert.IsTrue(mapObject.IsAvailable);
        }

        private (MapObjectGameObject mapObject, Collider rayCollider) Build(Guid mapObjectGuid)
        {
            // 生成prefabと同じく子にレイターゲット(コライダー+マーカー)を持つ最小構成
            // Minimal shape matching generated prefabs: a child ray target with collider and marker
            _root = new GameObject("MapObjectDecorationRayTargetTestRoot");
            var rayTargetObject = new GameObject("RayTarget");
            rayTargetObject.transform.SetParent(_root.transform, false);
            var rayCollider = rayTargetObject.AddComponent<BoxCollider>();
            rayTargetObject.AddComponent<MapObjectRayTarget>();

            var mapObject = _root.AddComponent<MapObjectGameObject>();
            mapObject.SetRuntimeIdentity(1, mapObjectGuid.ToString());
            mapObject.Initialize(new GetMapObjectInfoProtocol.MapObjectsInfoMessagePack(1, false, 10));
            return (mapObject, rayCollider);
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectDecorationRayTargetTest"`
Expected: 装飾物側が `rayCollider.enabled == true` で FAIL（`IsAvailable` も true）。

- [x] **Step 3: `MapObjectRayTarget` に `SetInteractable` を足す**

```csharp
using Client.Game.InGame.Mining;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectRayTarget : MonoBehaviour, IMiningRayTarget
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }

        public IMiningTargetObject MiningTargetObject => MapObjectGameObject;

        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            MapObjectGameObject = mapObjectGameObject;
        }

        // 装飾物はレイに乗せない。歩行用の物理コライダーは別オブジェクトなので影響しない
        // A decoration stays off the ray; the walking collider lives on another object and is untouched
        public void SetInteractable(bool interactable)
        {
            GetComponent<Collider>().enabled = interactable;
        }
    }
}
```

（既存の `using` は現ファイルのものを維持する。`Collider` は `UnityEngine`）

- [x] **Step 4: `MapObjectGameObject` を変更する**

`IsAvailable` を差し替える:

```csharp
        // マスタ欠損と装飾物(None)は対象として扱わない
        // A master-less object and a decoration (None) are not targets
        public bool IsAvailable => !IsDestroyed && MapObjectMasterElement != null && !IsDecoration;

        private bool IsDecoration => MapObjectMasterElement.MiningType == MapObjectMasterElement.MiningTypeConst.None;
```

`Initialize` のレイターゲット初期化ループを差し替える:

```csharp
            // 開幕スキットの非活性窓で生成される近傍個体があるため、非活性の子も走査する（2026-08-23裁定）
            // Near-field objects can be born inside the opening skit's inactive window, so inactive children are scanned too (adjudicated 2026-08-23)
            var rayTargets = GetComponentsInChildren<MapObjectRayTarget>(true);
            foreach (var rayTarget in rayTargets)
            {
                rayTarget.Initialize(this);
                rayTarget.SetInteractable(!IsDecoration);
            }
```

`TryBeginHandMining` は先頭の `if (!IsAvailable) return MiningStartOutcome.Unavailable;` が `IsDecoration` を含むので変更不要（`MiningMiningParam` キャストに到達しない）。

- [x] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObjectDecorationRayTargetTest|MapObjectHpBarScaleTest|MiningFocusStateTest|MiningAimTest"`
Expected: 全件 PASS。

- [x] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectRayTarget.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectDecorationRayTargetTest.cs moorestech_client/Assets/Scripts/Client.Tests/Map/MapObjectDecorationRayTargetTest.cs.meta
git commit -m "feat: クライアントがminingType Noneの装飾物をレイターゲットから外す"
```

---

### Task 6: マスタデータ39件を `None` にする（moorestech_master リポジトリ）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`
- Test: なし（Pythonアサーションで検証）

**Interfaces:**
- Consumes: Task 1 の `None`（スキーマ）
- Produces: `feature/decoration-map-objects-none` ブランチのpush済みコミットハッシュ。Task 7 が使う

- [x] **Step 1: master リポジトリでブランチを切る**

```bash
cd ../moorestech_master
git fetch origin
git switch -c feature/decoration-map-objects-none origin/master
git log --oneline -1
git status --short
```

Expected: HEADが `origin/master` の最新（2026-08-30時点 `bb00878`）。`git status --short` は空。

- [x] **Step 2: 書き換えスクリプトを `<scratchpad>/set_none.py` に書いて実行する**

```python
# 39件をminingType Noneの装飾物へ。それ以外は触らない
import json

PATH = "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"
TARGETS = {
    # ブッシュ系13件
    "dc8285a7-1816-470d-8b45-3446930d169b",  # ブッシュ
    "63899fad-7918-5cc0-98c6-185ab146c2d6",  # Mountains Bush1
    "c1907de3-d347-5e6a-b271-2876c444b1d2",  # Mountains Bush3
    "b497a900-7e17-5e87-b998-4db57a253cd0",  # Olivebush1
    "8b2e393a-662b-5202-9e11-5ff3d8b63a47",  # Olivebush2
    "1745a0ab-20f1-5d56-ad03-74136a0c049b",  # Olivebush3
    "bb461342-0548-5848-8543-59d547ebca70",  # Savanna Bush1
    "31a5b725-2467-5260-9b5c-9bc9b5a166df",  # Savanna Bush2
    "8e406597-fabe-5283-af1c-1ce5352c1770",  # Savanna Bush3
    "fcba078b-d003-565c-b70d-b3c9c97acb73",  # Brittlebush_1
    "bf9c7a87-f8e1-5a07-a700-3be1f3f615b7",  # Brittlebush_2
    "f01dc1c1-16fa-5d35-9b2e-1cd5fd3d3a7b",  # Brittlebush_3
    "17884ab6-424f-5abd-a6c5-716f57b63894",  # Brittlebush_4
    # メサ崖・地層系26件
    "7fc6f546-5d58-58d0-b15f-86fdd914a903", "b11b16a1-61fa-5cec-919b-bb5f1ac11892", "1ede18a0-e63d-5ac1-a19f-b7e8e8ccabe6",
    "fe4ab6b3-1d15-5edd-a9c5-6fbbeb78dfec", "e559d55b-a7b1-53a2-a0f7-91763cb415c0", "91f2d423-1595-502e-9477-6234b55f8b54",  # BigMesa_0-5
    "cfc9e94c-d9cc-58fb-878b-e5a03d944ff0", "2e233299-dfc0-5db1-8bdc-3790fca95dc6", "2e5fb411-03c4-5271-be7a-929fa2e98089",
    "3f57b892-b606-5737-ad75-fcc2101355ea", "572374fb-6c0a-5a2e-8d09-0dc6c17685e9", "6ad3879b-3ee2-52ee-bf8d-d73d449933cc",  # ThinMesa_0-5
    "e5076256-1de6-5b2f-90c9-dbfd4de161aa", "6ba9b62c-abc2-517c-8abf-cc0f875fd69a", "2b0608bf-ea9f-5e40-91d7-305aa496d56f",
    "5f02ead5-d1bb-5c89-bea8-670e0845f049", "61f9d17e-1810-57b8-aeb6-368d0151fb9d",  # StratMesaSharp_0-4
    "e47b4009-366a-527a-a74e-77a754c4682e", "eb42dcb0-563f-5b4f-b496-2f6b76ed1407", "135f1c4c-9a2f-5735-8db4-c25cf4cecf1f",
    "78c045aa-e8ea-56fc-a778-6d8fbb8a5977", "0332803b-43a4-5eaf-a0ac-cd18751d40e8", "173366a5-bd87-5183-9b47-f8d5960d6cd4",  # Strate_0-5
    "00034709-2c7c-5d96-8410-f53b081ab086", "a54bd8a5-d8ce-519e-89fb-482560957725", "0a32478f-ba41-5100-af41-df4ea3bf91e3",  # StrateCliff_0-2
}
assert len(TARGETS) == 39, len(TARGETS)

d = json.load(open(PATH, encoding="utf-8"))
hit = 0
for m in d["mapObjects"]:
    if m["mapObjectGuid"] not in TARGETS:
        continue
    m["miningType"] = "None"
    m["miningParam"] = {}
    m["earnItems"] = []
    hit += 1
assert hit == 39, hit
open(PATH, "w", encoding="utf-8").write(json.dumps(d, ensure_ascii=False, indent=2))
print("updated", hit)
```

Run: `cd <scratchpad> && python3 set_none.py`
Expected: `updated 39`

- [x] **Step 3: 期待状態をアサーションで検証する（`<scratchpad>/assert_none.py`）**

```python
import json, subprocess
PATH = "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"
d = json.load(open(PATH, encoding="utf-8"))
none = [m for m in d["mapObjects"] if m["miningType"] == "None"]
assert len(d["mapObjects"]) == 195, len(d["mapObjects"])
assert len(none) == 39, len(none)
assert all(m["earnItems"] == [] and m["miningParam"] == {} for m in none)
names = sorted(m["mapObjectName"] for m in none)
assert all(("bush" in n.lower()) or ("ブッシュ" in n) or any(k in n for k in ("BigMesa", "ThinMesa", "StratMesaSharp", "Strate_", "StrateCliff")) for n in names), names
others = [m for m in d["mapObjects"] if m["miningType"] != "None"]
assert all(m["earnItems"] for m in others)
# 差分が39要素の3キーだけであること（他行が動いていない）
stat = subprocess.run(["git", "-C", "../moorestech_master", "diff", "--numstat"], capture_output=True, text=True).stdout
print(stat)
print("OK", names)
```

Run: `python3 assert_none.py`
Expected: `OK [...]` と `--numstat` の行が map.json 1ファイルのみ。削除行数が異常に多い（数千行）場合は整形が崩れているので Step 2 の `indent=2` を確認する。

- [x] **Step 4: コミット・push・PR作成**

```bash
cd ../moorestech_master
git add server_v8/mods/moorestechAlphaMod_8/master/map.json
git commit -m "feat: ブッシュ系13件とメサ崖・地層系26件をminingType Noneの装飾物にする"
git push -u origin feature/decoration-map-objects-none
gh pr create --title "ブッシュ系とメサ崖をminingType Noneの装飾物にする" --body "$(cat <<'EOF'
## Summary
- ブッシュ系13件（ブッシュ・Mountains/Savanna Bush・Olivebush・Brittlebush）とメサ崖・地層系26件（BigMesa/ThinMesa/StratMesaSharp/Strate/StrateCliff）を `miningType: None`・`earnItems: []` にする
- コード側: moorestech の ADR 0043（docs/adr/0043-non-interactive-decoration-map-objects.md）

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
git rev-parse HEAD
```

Expected: PR URL が出力され、最後に40桁のハッシュが出る。これを Task 7 で使う。

---

### Task 7: master ピンを更新し、ゲーム起動でマスタ検証を通す

**Files:**
- Modify: `.moorestech-external-revisions.json:6`

**Interfaces:**
- Consumes: Task 6 のコミットハッシュ
- Produces: なし

- [x] **Step 1: ピンを書き換える**

`"key": "moorestech_master"` の `commitHash` を Task 6 のハッシュに置き換える。

```bash
python3 - <<'PY'
import json, subprocess
h = subprocess.run(["git", "-C", "../moorestech_master", "rev-parse", "HEAD"], capture_output=True, text=True).stdout.strip()
p = ".moorestech-external-revisions.json"
d = json.load(open(p))
[r for r in d["repositories"] if r["key"] == "moorestech_master"][0]["commitHash"] = h
open(p, "w").write(json.dumps(d, indent=4) + "\n")
print(h)
PY
git diff .moorestech-external-revisions.json
git -C ../moorestech_master ls-remote origin feature/decoration-map-objects-none
```

Expected: diff が `commitHash` の1行だけ。`ls-remote` が同じハッシュを返す（push済み＝R9）。整形（4スペース・末尾改行）が既存とずれて全行差分になったら、`git diff` を見て手で戻す。

- [x] **Step 2: 実マスタで検証を通す**

worktree の Unity で PlayMode を起動し、マスタロードで `[MapObjectMaster]` / `[ChallengeMaster]` のエラーが出ないことを確認する。

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`（PlayMode 起動→停止の後）
Expected: `MapObjectMaster` `ChallengeMaster` を含むエラー0件。特に「木を伐採」ピン（earnItem 原木）が `resolving to no MapObject` にならないこと（木は原木を落とし続けるので候補が残る）。

- [x] **Step 3: コミットする**

```bash
git add .moorestech-external-revisions.json
git commit -m "chore: moorestech_masterピンを装飾mapObject対応コミットへ更新する"
```

---

### Task 8: 全ブランチレビュー（必須・省略不可）

**Files:** ブランチ全体

- [ ] **Step 1: 関連テストを一括で回す**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "MapObject|Mining|ChallengeMasterValidation|GetMapDataProtocol"`
Expected: 全件 PASS。

- [ ] **Step 2: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` スキルを起動し、指摘の機械的修正を適用、設計判断は AskUserQuestion で仰ぐ。

- [ ] **Step 3: PR作成と撤収**

`pr-create` スキルでPRを作る（本文に master 側PRのURLを併記）。作成後 `moores-wt rm feature/decoration-map-objects-none` で worktree を畳む。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0043-non-interactive-decoration-map-objects.md`／裁定: `.decisions/2026-08-30-装飾mapObjectはminingType-Noneで表現しブッシュは完全に触れなくする.md`
- planning中の判断:
  - テスト用 `None` mapObject は master にだけ追加し配置には置かない。出所: agent前提（`GetMapDataProtocolTest` が配置6件を固定しているため。サーバーテストは `IMapObjectFactory.Create` で生成する）
  - クライアントのガードは `IsAvailable` に `IsDecoration` を畳み、`TryBeginHandMining` には分岐を足さない。出所: agent前提（同種条件は文脈が集まる側の一箇所へ、AGENTS.md）
  - HPバーは触らない。出所: agent前提（`SetFocused` 経由でのみ表示され、レイに乗らない個体はフォーカスされない）
  - `EarnItemsValidation` の None 限定化は ADR 0037 の検査を部分的に上書きする。出所: agent前提（ユーザー裁定「A 完全な装飾物」の帰結。ADR 0043「先行裁定との関係」に記載）
  - master 側ブランチは `origin/master`（bb00878）起点。現ピン `6fdf04d9` は origin/master に含まれる（#53マージ済み）。出所: agent前提
