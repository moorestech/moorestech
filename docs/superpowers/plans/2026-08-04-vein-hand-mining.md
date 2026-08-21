---
spec: docs/adr/0007-vein-as-hand-minable-target.md
---

# Vein手掘り対応 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 鉱脈（vein）そのものをツールで手掘りできるようにし、露頭ビジュアルを唯一の狙い先として序盤の鉱石入手経路を確立する。

**Architecture:** mapVeinsマスタに手掘り設定（handMiningType/ツール/ドロップ数）を追加し、サーバーは既存mapObject採掘プロトコルをTargetType分岐の採掘1本（va:mining）に統合、座標→GetOverVeinsで権威判定して1振り1ドロップを付与する。クライアントは採掘FSMのフォーカス対象を`IMiningTargetObject`に抽象化し、Layout応答のvein AABBから露頭GameObject（コライダ付き）を実行時生成して既存FSMに載せる。巨大HPの鉱脈mapObject4種は削除する。

**Tech Stack:** Unity / C# / Mooresmaster SourceGenerator / MessagePack / UniRx / UniTask / uloop CLI

## Global Constraints

- 鉱脈はHP・ダメージ概念なしの**1振り1ドロップ**（minCount〜maxCount個、速度はattackSpeedクールダウンのみ）。ドロップ物は`veinParam.itemGuid`が唯一の正（ADR-0007）
- 手掘り可能なのは序盤資源のみ: 石・小石・原木・粘土・銅・青銅・鉄・石炭。タングステン・fluid鉱脈はhandMiningType none（ADR-0007）
- 狙い先は露頭ビジュアルのみ。露頭はクライアントがvein AABB 1件につき1個、AABB中心XZの地表に実行時生成する純ビジュアル（サーバー非管理）（ADR-0007）
- プロトコルは手採採掘1ドメイン1本・TargetType enum分岐。サーバ権威（ADR-0004）維持（.claude/rules/server-protocol.md）
- `optional: true`原則禁止・`?? Default`フォールバック禁止・全JSON一括更新が正規手順（edit-schema CRITICAL）
- partial禁止・Func<>禁止・try-catch原則禁止・1ファイル200行以下・イベントはUniRx（AGENTS.md）
- サーバー時間計測はGameUpdaterティックのみ（AGENTS.md）
- .metaファイル手動作成禁止。プレハブ・シーンの変更はuloop execute-dynamic-code経由のみ（AGENTS.md）
- コード変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（AGENTS.md）
- 「Unity is reloading」エラー時は45秒待機してリトライ（AGENTS.md）
- ブランチ: master から `feature/vein-hand-mining` を切って作業する（現在の feature/misses には積まない）

---

## File Structure（全体マップ）

**スキーマ・マスタ（Task 1, 6, 12）**
- `VanillaSchema/map.yml` — mapVeinsに outcropAddressablePath / handMiningType / handMiningParam 追加
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGeneratorトリガ
- `moorestech_server/Assets/Scripts/Core.Master/Validator/MapVeinMasterUtil.cs` — handMining整合バリデーション
- mapVeinsを持つ全JSON3件（ForUnitTest / EditModeInPlayingTest / v8 mod）

**サーバー（Task 2〜5）**
- `moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IItemMapVein.cs` — VeinGuid追加
- `moorestech_server/Assets/Scripts/Game.Map/ItemMapVein.cs` / `ItemMapVeinDatastore.cs` — VeinGuid保持
- `moorestech_server/Assets/Scripts/Game.Map/MiningCooldownService.cs` — 新規（クールダウン共有）
- `moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs` — クールダウンを共有サービスへ委譲
- `moorestech_server/Assets/Scripts/Game.Map/VeinHandMiningService.cs` — 新規（vein採掘判定・ドロップ）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs` — MapObjectAcquisitionProtocol.csをgit mv+改名（va:mining・TargetType分岐）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs` — タグ登録更新
- `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — サービスDI登録

**クライアント（Task 5, 7, 9）**
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs` — 統合採掘送信API
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningTargetObject.cs` — 新規（採掘対象抽象）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningToolCandidate.cs` — 新規
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/` FSM群 — 対象型をinterfaceへ置換（git mv改名）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs` — IMiningTargetObject実装
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs` — 新規
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropRayTarget.cs` — 新規
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs` — 新規（Layout.MapVeins消費）
- `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` — Datastore配線

**テスト（Task 4, 5, 6, 10）**
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/VeinMiningProtocolTest.cs` — 新規
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MapObjectAcquisitionProtocolTest.cs` — 新MessagePackへ追従
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs` — バリデーションテスト追加
- `moorestech_client/Assets/Scripts/Client.Tests/Mining/OutcropMiningTargetTest.cs` — 新規

**アセット・実データ（Task 11, 12）**
- `moorestech_client/Assets/AddressableResources/Environment/` — 露頭プレハブ（既存4改修+7新規、uloop EDC経由）
- `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` — handMining設定・鉱脈mapObject4種削除
- `../moorestech_master/server_v8/map/map.json` — 鉱脈mapObjectインスタンス約100件削除
- `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json` — mapObjectPin 2件の対応（要裁定）

---

### Task 1: mapVeinsスキーマ拡張とJSON一括更新

**Files:**
- Modify: `VanillaSchema/map.yml`（mapVeins items末尾）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`

**Interfaces:**
- Produces: 生成型 `MapVeinMasterElement.OutcropAddressablePath`(string) / `.HandMiningType`(string) / `.HandMiningParam`(object)、switch生成クラス `NoneHandMiningParam` / `MinableHandMiningParam`（`.HandMiningTools`: `HandMiningToolsElement[]`{`ToolItemGuid`, `AttackSpeed`} / `.MinCount` / `.MaxCount`）。後続タスクはこの型名を前提とする（コンパイル後に実名を確認し、ズレていれば後続タスク内の型名を実名に合わせる）

**注意:** ツール配列のキーは`handMiningTools`とする。`miningTools`にするとmapObjects側の生成クラス`MiningToolsElement`と同名クラスが同一モジュールに二重生成され衝突するため。

- [ ] **Step 1: map.ymlのmapVeins itemsに3プロパティを追加する**

`VanillaSchema/map.yml` の `veinParam` switchブロックの直後（mapVeins itemsのproperties末尾）に追記:

```yaml
    - key: outcropAddressablePath
      type: string
      default: Vanilla/Environment/
    - key: soundEffectType
      type: enum
      options:
      - tree
      - stone
    - key: handMiningType
      type: enum
      options:
      - none
      - minable
    - key: handMiningParam
      switch: ./handMiningType
      cases:
      - when: none
        type: object
        optional: true
        properties: []
      - when: minable
        type: object
        properties:
        - key: handMiningTools
          type: array
          items:
            type: object
            properties:
            - key: toolItemGuid
              type: uuid
              foreignKey:
                schemaId: items
                foreignKeyIdPath: /data/[*]/itemGuid
                displayElementPath: /data/[*]/name
            - key: attackSpeed
              type: number
              default: 1
        - key: minCount
          type: integer
          default: 1
        - key: maxCount
          type: integer
          default: 1
```

（`when: none` の `optional: true` 空objectは同ファイルmapObjects側 `when: PickUp` の既存前例に一致。フィールド単位のoptionalではなくswitch空caseの表現なのでedit-schema CRITICALには抵触しない。soundEffectTypeは露頭の打撃/破壊音をマスタ駆動にするための追加 — 原木鉱脈はtree音であり固定stoneだと1振りごとに鳴る可聴退行になる）

- [ ] **Step 2: _CompileRequester.csのdummyTextを変更してSourceGeneratorをトリガする**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 値を任意の新文字列（例: `"vein-hand-mining-1"`）に変更する。

- [ ] **Step 3: テストmod2件のmapVeins JSONを更新する**

`ForUnitTest/mods/forUnitTest/master/map.json` の mapVeins を以下に置換（IronVeinのみminable。toolItemGuidは既存採掘テストのツール、attackSpeedも既存テストの0.2に合わせる）:

```json
"mapVeins": [
  {"veinGuid": "11111111-0000-0000-0000-000000000001", "veinName": "test:IronVein", "veinType": "item",
   "veinParam": {"itemGuid": "00000000-0000-0000-1234-000000000001"},
   "outcropAddressablePath": "Vanilla/Environment/IronVein", "soundEffectType": "stone",
   "handMiningType": "minable",
   "handMiningParam": {"handMiningTools": [{"toolItemGuid": "00000000-0000-0000-1234-000000000001", "attackSpeed": 0.2}], "minCount": 1, "maxCount": 1}},
  {"veinGuid": "11111111-0000-0000-0000-000000000002", "veinName": "test:WaterVein", "veinType": "fluid",
   "veinParam": {"fluidGuid": "00000000-0000-0000-1234-000000000001"},
   "outcropAddressablePath": "Vanilla/Environment/WaterVein", "soundEffectType": "stone",
   "handMiningType": "none", "handMiningParam": null},
  {"veinGuid": "11111111-0000-0000-0000-000000000003", "veinName": "test:SteamVein", "veinType": "fluid",
   "veinParam": {"fluidGuid": "00000000-0000-0000-1234-000000000002"},
   "outcropAddressablePath": "Vanilla/Environment/SteamVein", "soundEffectType": "stone",
   "handMiningType": "none", "handMiningParam": null}
]
```

`EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/map.json` は既存mapVeins各要素に `"outcropAddressablePath": "Vanilla/Environment/StoneVein"`, `"soundEffectType": "stone"`, `"handMiningType": "none"`, `"handMiningParam": null` を追記する（内容確認の上、item veinが有ればaddressは対応する既存プレハブ名にする）。

- [ ] **Step 4: v8 mod master map.jsonのmapVeinsを更新する**

`../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` のmapVeins全11件に handMiningType/handMiningParam を追加する。方針:
- minable（石・小石・原木・粘土・銅・青銅・鉄・石炭の8件）: handMiningToolsは既存「石鉱脈」mapObjectのminingToolsから転記（石の斧 attackSpeed 1 / 石器 attackSpeed 3。damageは捨てる）、minCount 1 / maxCount 1
- none（タングステン・水・原油の3件）: `"handMiningType": "none", "handMiningParam": null`
- outcropAddressablePathは既存値のまま（Task 11で実プレハブaddressに更新する）

pythonスクリプトで機械的に適用する（石の斧/石器のguidは石鉱脈mapObjectのminingToolsから読み取る。手打ちしない）:

```python
import json
p = '../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json'
d = json.load(open(p))
stone = next(o for o in d['mapObjects'] if o['mapObjectName'] == '石鉱脈')
tools = [{"toolItemGuid": t['toolItemGuid'], "attackSpeed": t['attackSpeed']}
         for t in stone['miningParam']['miningTools']]
minable = {'石鉱脈', '小石鉱脈', '原木鉱脈', '粘土鉱脈', '銅の鉱石鉱脈', '青銅の鉱石鉱脈', '鉄鉱石鉱脈', '石炭鉱脈'}
for v in d['mapVeins']:
    # 原木鉱脈だけtree音（削除予定の原木鉱脈mapObjectのsoundEffectType: treeを引き継ぐ）
    v['soundEffectType'] = 'tree' if v['veinName'] == '原木鉱脈' else 'stone'
    if v['veinName'] in minable:
        v['handMiningType'] = 'minable'
        v['handMiningParam'] = {"handMiningTools": tools, "minCount": 1, "maxCount": 1}
    else:
        v['handMiningType'] = 'none'
        v['handMiningParam'] = None
json.dump(d, open(p, 'w'), ensure_ascii=False, indent=4)
```

- [ ] **Step 5: インラインJSONを持つ既存テストを新必須フィールドへ追従させる**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs:65-67` の `実在しないitemGuidの鉱脈はバリデーションで失敗する` はJToken文字列でmapVeinsを組むため、新必須フィールドが無いとローダーで死ぬ。JSONに `""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",""handMiningType"":""none"",""handMiningParam"":null` を追記する。他にインラインでmapVeins JSONを組むテストが無いか `grep -rn '"mapVeins"' moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Tests --include="*.cs"` で全数確認し、あれば同様に追従する。

- [ ] **Step 6: コンパイルして生成型を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。生成型 `MinableHandMiningParam` / `HandMiningToolsElement` が存在すること（`grep -rn "MinableHandMiningParam" moorestech_client/Library/Bee 2>/dev/null | head -1` でも、後続タスクのコード参照コンパイルでも確認可。実名がズレた場合は後続タスクの型名を実名へ揃える）

- [ ] **Step 7: コミット**

```bash
git add VanillaSchema/map.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Tests.Module moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: mapVeinsに手掘り設定(handMiningType/handMiningTools)と露頭パスを追加"
cd ../moorestech_master && git add server_v8/mods/moorestechAlphaMod_8/master/map.json && git commit -m "feat: v8鉱脈に手掘り設定を追加（序盤資源minable・タングステン/fluid none）" && cd -
```

---

### Task 2: IItemMapVeinにVeinGuidを追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Map.Interface/Vein/IItemMapVein.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map/ItemMapVein.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map/ItemMapVeinDatastore.cs:38`

**Interfaces:**
- Produces: `IItemMapVein.VeinGuid`(Guid) — Task 4のVeinHandMiningServiceがマスタ引きに使う

- [ ] **Step 1: interfaceと実装にVeinGuidを追加する**

`IItemMapVein.cs`:

```csharp
using System;
using Core.Master;
using UnityEngine;

namespace Game.Map.Interface.Vein
{
    public interface IItemMapVein
    {
        public Guid VeinGuid { get; }
        public ItemId VeinItemId { get; }

        public Vector3Int VeinRangeMin { get; }
        public Vector3Int VeinRangeMax { get; }
    }
}
```

`ItemMapVein.cs` — `public Guid VeinGuid { get; }` を追加しコンストラクタ先頭引数に `Guid veinGuid` を追加。`ItemMapVeinDatastore.cs:38` の生成を `new ItemMapVein(veinJson.VeinGuid, itemId, veinJson.MinPosition, veinJson.MaxPosition)` に変更。

- [ ] **Step 2: コンパイル確認・コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（IItemMapVeinの実装はItemMapVeinのみ、他に`new ItemMapVein(`は無い）

```bash
git add moorestech_server/Assets/Scripts/Game.Map.Interface moorestech_server/Assets/Scripts/Game.Map
git commit -m "feat: IItemMapVeinにVeinGuidを追加しマスタ逆引きを可能にする"
```

---

### Task 3: クールダウンをMiningCooldownServiceへ抽出

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Map/MiningCooldownService.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map/MapObjectMiningService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:155`付近

**Interfaces:**
- Produces: `MiningCooldownService.IsInCooldown(int playerId, double attackSpeed): bool` / `RecordAttack(int playerId): void` — Task 4のVeinHandMiningServiceと共有し、プレイヤー1振り制限を全採掘共通にする

- [ ] **Step 1: MiningCooldownService.csを新規作成する**

```csharp
using System.Collections.Generic;
using Core.Update;

namespace Game.Map
{
    /// <summary>
    ///     手採採掘のプレイヤー単位クールダウン。mapObject採掘とvein採掘で共有し1振り制限を全採掘共通にする
    ///     Per-player cooldown for hand mining; shared by mapObject and vein mining to enforce one swing at a time
    /// </summary>
    public class MiningCooldownService
    {
        // クールダウン判定の許容率。クライアントはattackSpeed間隔ちょうどで送るためジッタ余裕を持たせる
        // Cooldown tolerance; clients send at exactly attackSpeed intervals, so allow jitter
        private const double CooldownMarginRate = 0.9;

        // 1プレイヤー1振りを保証する最終打撃tick
        // Last-hit ticks enforcing one swing at a time per player
        private readonly Dictionary<int, ulong> _lastAttackTicks = new();

        public bool IsInCooldown(int playerId, double attackSpeed)
        {
            if (!_lastAttackTicks.TryGetValue(playerId, out var lastAttackTick)) return false;
            return GameUpdater.CurrentTick - lastAttackTick < GameUpdater.SecondsToTicks(attackSpeed * CooldownMarginRate);
        }

        public void RecordAttack(int playerId)
        {
            _lastAttackTicks[playerId] = GameUpdater.CurrentTick;
        }
    }
}
```

- [ ] **Step 2: MapObjectMiningServiceを委譲へ書き換える**

- `CooldownMarginRate`定数・`_lastAttackTicks`フィールド・ローカル関数`IsInCooldown`を削除
- コンストラクタ `public MapObjectMiningService(MiningCooldownService cooldownService)` を追加し `private readonly MiningCooldownService _cooldownService;` に保持
- `TryAttack`内の判定を `if (_cooldownService.IsInCooldown(playerId, usableTool.AttackSpeed)) return MiningAttackResult.CooldownNotElapsed;` に、記録を `_cooldownService.RecordAttack(playerId);` に置換

- [ ] **Step 3: DI登録を追加する**

`MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<MapObjectMiningService>();` の直前に `services.AddSingleton<MiningCooldownService>();` を追加。

- [ ] **Step 4: コンパイル＋既存採掘テストが緑のままであることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectAcquisitionProtocolTest|MapObjectMiningDestroyGuardTest"`
Expected: 全件PASS（挙動不変のリファクタリング）

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Map moorestech_server/Assets/Scripts/Server.Boot
git commit -m "refactor: 採掘クールダウンをMiningCooldownServiceへ抽出しvein採掘と共有可能にする"
```

---

### Task 4: VeinHandMiningService（vein採掘判定・1振り1ドロップ）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Map/VeinHandMiningService.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/map/map.json`（fluid veinのAABBがitem veinと重ならないことを確認。現状 IronVein(0,5,0)〜(0,5,0)・WaterVein(0,0,0)〜(10,0,0) はY違いで重複せずそのまま使える）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/VeinMiningProtocolTest.cs`（テスト本体はTask 5で作成。本タスクではサービス単体の失敗テストを同ファイルに先行して書く）

**Interfaces:**
- Consumes: `IItemMapVein.VeinGuid`(Task 2) / `MiningCooldownService`(Task 3) / 生成型 `MinableHandMiningParam`(Task 1)
- Produces: `VeinMiningResult` enum { Success, NoMinableVein, NoTool, ToolMismatch, CooldownNotElapsed } / `VeinHandMiningService.TryMine(int playerId, Vector3Int position, IItemStack equippedItem, out List<IItemStack> earnedItems): VeinMiningResult`

- [ ] **Step 1: 失敗するテストを書く（サービス直叩き）**

`Tests/CombinedTest/Server/PacketTest/VeinMiningProtocolTest.cs` を新規作成し、まずサービス単体テストを置く:

```csharp
using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Map;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     vein手掘りの権威判定（座標→vein解決・ツール照合・1振り1ドロップ・クールダウン共有）を検証する
    ///     Verifies vein hand-mining authority: position→vein resolution, tool matching, per-swing drops, shared cooldown
    /// </summary>
    public class VeinMiningProtocolTest
    {
        private const int PlayerId = 0;

        // ForUnitTestマスタのIronVein(minable, tool=1234-0001, attackSpeed0.2)と対応座標
        // ForUnitTest master's IronVein (minable, tool 1234-0001, attackSpeed 0.2) and a position inside it
        private static readonly Vector3Int InsideIronVein = new(0, 5, 0);
        private static readonly Vector3Int OutsideAnyVein = new(500, 500, 500);
        private static readonly Vector3Int InsideFluidVein = new(5, 0, 0);
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnmatchedToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");
        private const double ExpectedAttackSpeed = 0.2;

        [Test]
        public void 対応ツール装備時のみvein上の座標で鉱石が1振りごとに入る()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // 素手はNoTool
            // Bare hands yield NoTool
            Assert.AreEqual(VeinMiningResult.NoTool, miningService.TryMine(PlayerId, InsideIronVein, equipped, out _));

            // 非対応ツールはToolMismatch
            // A non-matching tool yields ToolMismatch
            EquipTool(playerInventory, UnmatchedToolItemGuid);
            Assert.AreEqual(VeinMiningResult.ToolMismatch, miningService.TryMine(PlayerId, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), out _));

            // 対応ツールでminCount〜maxCount（テストマスタは1〜1固定）個ドロップする
            // The matching tool drops minCount..maxCount items (fixed 1..1 in the test master)
            EquipTool(playerInventory, ToolItemGuid);
            Assert.AreEqual(VeinMiningResult.Success, miningService.TryMine(PlayerId, InsideIronVein, playerInventory.EquipmentInventory.GetSelectedItem(), out var earnedItems));
            Assert.AreEqual(1, earnedItems.Sum(item => item.Count));
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(ToolItemGuid), earnedItems[0].Id);
        }

        [Test]
        public void vein外とfluid_veinとnone設定では掘れない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var miningService = serviceProvider.GetService<VeinHandMiningService>();
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // vein AABBの外は掘れない
            // Positions outside every vein AABB are not minable
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, OutsideAnyVein, equipped, out _));

            // fluid veinはItemMapVeinDatastoreの対象外なので同じくNoMinableVein
            // Fluid veins are outside ItemMapVeinDatastore, so also NoMinableVein
            Assert.AreEqual(VeinMiningResult.NoMinableVein, miningService.TryMine(PlayerId, InsideFluidVein, equipped, out _));
        }

        [Test]
        public void mapObject採掘とクールダウンを共有する()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var veinService = serviceProvider.GetService<VeinHandMiningService>();
            EquipTool(playerInventory, ToolItemGuid);
            var equipped = playerInventory.EquipmentInventory.GetSelectedItem();

            // 1振り目成功→クールダウン内の2振り目は弾かれる→tick経過で再度成功
            // First swing lands, second within cooldown is dropped, and it lands again after ticks pass
            Assert.AreEqual(VeinMiningResult.Success, veinService.TryMine(PlayerId, InsideIronVein, equipped, out _));
            Assert.AreEqual(VeinMiningResult.CooldownNotElapsed, veinService.TryMine(PlayerId, InsideIronVein, equipped, out _));
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(ExpectedAttackSpeed) + 1);
            Assert.AreEqual(VeinMiningResult.Success, veinService.TryMine(PlayerId, InsideIronVein, equipped, out _));
        }

        private void EquipTool(PlayerInventoryData playerInventory, Guid toolItemGuid)
        {
            var toolItemId = MasterHolder.ItemMaster.GetItemId(toolItemGuid);
            playerInventory.EquipmentInventory.SetItem(0, toolItemId, 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `VeinHandMiningService` / `VeinMiningResult` 未定義のコンパイルエラー

- [ ] **Step 3: VeinHandMiningService.csを実装する**

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Mooresmaster.Model.MapModule;
using UnityEngine;
using Random = System.Random;

namespace Game.Map
{
    public enum VeinMiningResult
    {
        Success,
        NoMinableVein,
        NoTool,
        ToolMismatch,
        CooldownNotElapsed,
    }

    /// <summary>
    ///     vein手掘りのサーバ権威判定。座標→vein解決・ツール照合・1振り1ドロップを担う
    ///     Server-authoritative vein hand mining: position→vein resolution, tool matching, one drop per swing
    /// </summary>
    public class VeinHandMiningService
    {
        private readonly MiningCooldownService _cooldownService;
        private readonly Random _random = new();

        public VeinHandMiningService(MiningCooldownService cooldownService)
        {
            _cooldownService = cooldownService;
        }

        public VeinMiningResult TryMine(int playerId, Vector3Int position, IItemStack equippedItem, out List<IItemStack> earnedItems)
        {
            earnedItems = null;

            // 座標上のitem veinからminable設定のものを探す
            // Find a minable-configured item vein over the position
            if (!TryFindMinableVein(position, out var vein, out var minableParam)) return VeinMiningResult.NoMinableVein;

            // 素手はどのツールにも一致しない
            // Bare hands match no tools
            if (equippedItem.Id == ItemMaster.EmptyItemId) return VeinMiningResult.NoTool;

            // 装備中ツールをhandMiningToolsと照合する
            // Match the equipped tool against handMiningTools
            if (!TryResolveUsableTool(equippedItem.Id, minableParam.HandMiningTools, out var usableTool)) return VeinMiningResult.ToolMismatch;

            // mapObject採掘と共有のクールダウンで1振り制限を守る
            // The cooldown shared with mapObject mining enforces one swing at a time
            if (_cooldownService.IsInCooldown(playerId, usableTool.AttackSpeed)) return VeinMiningResult.CooldownNotElapsed;

            _cooldownService.RecordAttack(playerId);
            earnedItems = CreateEarnedItems(vein.VeinItemId, minableParam);
            return VeinMiningResult.Success;

            #region Internal

            bool TryFindMinableVein(Vector3Int pos, out Game.Map.Interface.Vein.IItemMapVein foundVein, out MinableHandMiningParam foundParam)
            {
                foundVein = null;
                foundParam = null;
                foreach (var overVein in ServerContext.ItemMapVeinDatastore.GetOverVeins(pos))
                {
                    var element = MasterHolder.MapVeinMaster.GetElementOrNull(overVein.VeinGuid);
                    if (element.HandMiningParam is not MinableHandMiningParam minable) continue;
                    foundVein = overVein;
                    foundParam = minable;
                    return true;
                }

                return false;
            }

            bool TryResolveUsableTool(ItemId equippedItemId, HandMiningToolsElement[] tools, out HandMiningToolsElement usable)
            {
                usable = null;
                var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
                foreach (var tool in tools)
                {
                    if (tool.ToolItemGuid != equippedItemGuid) continue;
                    usable = tool;
                    return true;
                }

                return false;
            }

            List<IItemStack> CreateEarnedItems(ItemId itemId, MinableHandMiningParam param)
            {
                // 1振りごとにminCount〜maxCountの一様抽選。スタック分割はInsertItem側が行うため個数は1スタックで足りる
                // Roll minCount..maxCount uniformly per swing; InsertItem handles stack splitting downstream
                var count = _random.Next(param.MinCount, param.MaxCount + 1);
                return new List<IItemStack> { ServerContext.ItemStackFactory.Create(itemId, count) };
            }

            #endregion
        }
    }
}
```

（maxCountがアイテムの最大スタックを超える運用はマスタ調整で行わない前提。超える設定が必要になったらVanillaStaticMapObjectのスタック分割前例を移植する）

- [ ] **Step 4: DI登録を追加する**

`MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<MapObjectMiningService>();` の直後に `services.AddSingleton<VeinHandMiningService>();` を追加。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinMiningProtocolTest"`
Expected: 3件PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Map moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Tests
git commit -m "feat: VeinHandMiningServiceを追加しveinの1振り1ドロップ手掘りをサーバ権威で判定する"
```

---

### Task 5: 採掘プロトコル統合（va:mining・TargetType分岐）

**Files:**
- Rename: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapObjectAcquisitionProtocol.cs` → `MiningProtocol.cs`（.metaも同時にgit mv）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:45`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs:69-73`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MapObjectAcquisitionProtocolTest.cs`（SendAttackのMessagePack差し替え）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/MapObjectMiningDestroyGuardTest.cs`（同上。旧MessagePack参照があれば）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/VeinMiningProtocolTest.cs`（プロトコル経由テスト追記）

**Interfaces:**
- Consumes: `VeinHandMiningService.TryMine`(Task 4)
- Produces: タグ `va:mining` / `MiningTargetType` enum { MapObject, Vein } / `MiningProtocol.MiningProtocolMessagePack(int playerId, MiningTargetType targetType, int instanceId, Vector3IntMessagePack veinPosition)` / クライアント `VanillaApiSendOnly.AttackMapObject(int instanceId)`（既存シグネチャ維持）・`VanillaApiSendOnly.MineVein(Vector3Int position)`（新設。Task 9の露頭が呼ぶ）

- [ ] **Step 1: プロトコル経由の失敗テストを書く**

`VeinMiningProtocolTest.cs` に追記:

```csharp
        [Test]
        public void プロトコル経由でvein採掘するとインベントリに鉱石が入る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            EquipTool(playerInventory, ToolItemGuid);

            // 座標をTargetType=Veinで送るとサーバがGetOverVeinsで解決しドロップする
            // Sending a position with TargetType=Vein makes the server resolve via GetOverVeins and drop items
            var messagePack = new Server.Protocol.PacketResponse.MiningProtocol.MiningProtocolMessagePack(
                PlayerId, Server.Protocol.PacketResponse.MiningProtocol.MiningTargetType.Vein, 0,
                new Server.Util.MessagePack.Vector3IntMessagePack(InsideIronVein));
            packet.GetPacketResponse(MessagePack.MessagePackSerializer.Serialize(messagePack), new Server.Protocol.PacketResponseContext(null));

            var ironItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            var count = Enumerable.Range(0, playerInventory.MainOpenableInventory.GetSlotSize())
                .Where(slot => playerInventory.MainOpenableInventory.GetItem(slot).Id == ironItemId)
                .Sum(slot => playerInventory.MainOpenableInventory.GetItem(slot).Count);
            Assert.AreEqual(1, count);
        }
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MiningProtocol` 未定義エラー

- [ ] **Step 3: MiningProtocol.csへ改名・統合実装する**

```bash
git mv moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapObjectAcquisitionProtocol.cs moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs
git mv moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapObjectAcquisitionProtocol.cs.meta moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MiningProtocol.cs.meta
```

クラスを以下へ書き換える:

```csharp
using System;
using Game.Context;
using Game.Map;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     手採採掘プロトコル。mapObject採掘とvein採掘をTargetTypeで分岐する1本のドメインプロトコル
    ///     Hand-mining protocol; one domain protocol switching on TargetType between mapObject and vein mining
    /// </summary>
    public class MiningProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:mining";

        public enum MiningTargetType
        {
            MapObject,
            Vein,
        }

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly MapObjectUpdateEventPacket _mapObjectUpdateEventPacket;
        private readonly MapObjectMiningService _mapObjectMiningService;
        private readonly VeinHandMiningService _veinHandMiningService;

        public MiningProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _mapObjectUpdateEventPacket = serviceProvider.GetService<MapObjectUpdateEventPacket>();
            _mapObjectMiningService = serviceProvider.GetService<MapObjectMiningService>();
            _veinHandMiningService = serviceProvider.GetService<VeinHandMiningService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<MiningProtocolMessagePack>(payload);
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();

            // TargetTypeで分岐し未知値はフォールバックせず例外にする
            // Dispatch on TargetType; unknown values throw instead of falling back
            var earnedItems = data.TargetType switch
            {
                MiningTargetType.MapObject => AttackMapObject(),
                MiningTargetType.Vein => MineVein(),
                _ => throw new ArgumentOutOfRangeException(nameof(data.TargetType), data.TargetType, null),
            };

            if (earnedItems != null)
                foreach (var earnItem in earnedItems)
                    playerInventory.MainOpenableInventory.InsertItem(earnItem);

            return null;

            #region Internal

            System.Collections.Generic.List<Core.Item.Interface.IItemStack> AttackMapObject()
            {
                var mapObject = ServerContext.MapObjectDatastore.Get(data.InstanceId);
                var result = _mapObjectMiningService.TryAttack(data.PlayerId, mapObject, equippedItem, out var items);
                if (result != MiningAttackResult.Success)
                {
                    Debug.Log($"Mining attack rejected. playerId:{data.PlayerId} instanceId:{data.InstanceId} result:{result}");
                    return null;
                }

                // HP更新イベントを送信（破壊されていない場合のみ）
                // Send the HP update event (only while not destroyed)
                if (!mapObject.IsDestroyed) _mapObjectUpdateEventPacket.SendHpUpdateEvent(mapObject);
                return items;
            }

            System.Collections.Generic.List<Core.Item.Interface.IItemStack> MineVein()
            {
                // veinは無限資源でサーバ側可変状態を持たないためイベント送出は無い
                // Veins are infinite with no server-side mutable state, so no event is emitted
                var result = _veinHandMiningService.TryMine(data.PlayerId, data.VeinPosition.Vector3Int, equippedItem, out var items);
                if (result != VeinMiningResult.Success)
                {
                    Debug.Log($"Vein mining rejected. playerId:{data.PlayerId} position:{data.VeinPosition.Vector3Int} result:{result}");
                    return null;
                }

                return items;
            }

            #endregion
        }

        [MessagePackObject]
        public class MiningProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public MiningTargetType TargetType { get; set; }
            [Key(4)] public int InstanceId { get; set; }
            [Key(5)] public Vector3IntMessagePack VeinPosition { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public MiningProtocolMessagePack() { }

            public MiningProtocolMessagePack(int playerId, MiningTargetType targetType, int instanceId, Vector3IntMessagePack veinPosition)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                TargetType = targetType;
                InstanceId = instanceId;
                VeinPosition = veinPosition;
            }
        }
    }
}
```

（`Vector3IntMessagePack` のメンバー名は `Server.Util/MessagePack/Vector3IntMessagePack.cs` を実装時に確認し、`Vector3Int` プロパティ名が異なればそれに合わせる。usingを整理し`System.Collections.Generic`/`Core.Item.Interface`はファイル先頭usingへ移動して短縮してよい）

- [ ] **Step 4: 登録タグとクライアント送信を更新する**

- `PacketResponseCreator.cs:45` → `_packetResponseDictionary.Add(MiningProtocol.ProtocolTag, new MiningProtocol(serviceProvider));`
- `VanillaApiSendOnly.cs` の `AttackMapObject` を新MessagePackで置換し、`MineVein` を追加:

```csharp
        public void AttackMapObject(int mapObjectInstanceId)
        {
            var request = new MiningProtocol.MiningProtocolMessagePack(
                _playerConnectionSetting.PlayerId, MiningProtocol.MiningTargetType.MapObject, mapObjectInstanceId, new Vector3IntMessagePack(Vector3Int.zero));
            _packetSender.Send(request);
        }

        public void MineVein(Vector3Int position)
        {
            var request = new MiningProtocol.MiningProtocolMessagePack(
                _playerConnectionSetting.PlayerId, MiningProtocol.MiningTargetType.Vein, 0, new Vector3IntMessagePack(position));
            _packetSender.Send(request);
        }
```

（フィールド名・送信メソッドは既存`AttackMapObject`実装の形式に合わせる）

- [ ] **Step 5: 既存テストのSendAttackを新MessagePackへ追従させる**

`MapObjectAcquisitionProtocolTest.cs` の `SendAttack` を:

```csharp
        private void SendAttack(PacketResponseCreator packet, int instanceId)
        {
            var messagePack = new MiningProtocol.MiningProtocolMessagePack(
                PlayerId, MiningProtocol.MiningTargetType.MapObject, instanceId, new Server.Util.MessagePack.Vector3IntMessagePack(UnityEngine.Vector3Int.zero));
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), new PacketResponseContext(null));
        }
```

に置換（`using static ...MapObjectAcquisitionProtocol` は `MiningProtocol` に変更）。`MapObjectMiningDestroyGuardTest.cs` も同様に旧MessagePack参照があれば置換。他の旧型参照は `grep -rn "GetMapObjectProtocolProtocolMessagePack\|mapObjectInfoAcquisition" moorestech_server moorestech_client --include="*.cs"` で全数確認し置換する。

- [ ] **Step 6: 全採掘テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "VeinMiningProtocolTest|MapObjectAcquisitionProtocolTest|MapObjectMiningDestroyGuardTest"`
Expected: 全件PASS

- [ ] **Step 7: コミット**

```bash
git add -A moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests moorestech_client/Assets/Scripts/Client.Network
git commit -m "feat: 採掘プロトコルをva:miningに統合しTargetType分岐でvein採掘を追加"
```

---

### Task 6: MapVeinMasterUtilのhandMiningバリデーション

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/MapVeinMasterUtil.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/Map/MapVeinMasterTest.cs`

**Interfaces:**
- Consumes: 生成型 `MinableHandMiningParam` / `FluidVeinParam`(既存)

- [ ] **Step 1: 失敗するテストを書く（既存のJToken組み立て形式に合わせる）**

`MapVeinMasterTest.cs` に追記（既存 `実在しないitemGuidの鉱脈はバリデーションで失敗する` と同形式。toolItemGuid `00000000-0000-0000-1234-000000000001` はForUnitTestに実在するアイテム）:

```csharp
        [Test]
        public void fluid鉱脈をminableにするとバリデーションで失敗する()
        {
            // fluid veinにminableを与えるとValidateがfalseを返す
            // Making a fluid vein minable must fail validation
            var json = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000002"",""veinName"":""badFluid"",""veinType"":""fluid"",
                 ""veinParam"":{""fluidGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/WaterVein"",""soundEffectType"":""stone"",
                 ""handMiningType"":""minable"",
                 ""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1}],""minCount"":1,""maxCount"":1}}]}");
            var master = new MapVeinMaster(json);
            Assert.IsFalse(master.Validate(out var logs));
            Assert.IsTrue(logs.Contains("badFluid"));
        }

        [Test]
        public void handMiningToolsが空またはカウント不正はバリデーションで失敗する()
        {
            // ツール空配列とminCount>maxCountはどちらも失敗する
            // An empty tool array and minCount > maxCount both fail validation
            var emptyToolsJson = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000003"",""veinName"":""noTools"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",
                 ""handMiningType"":""minable"",
                 ""handMiningParam"":{""handMiningTools"":[],""minCount"":1,""maxCount"":1}}]}");
            Assert.IsFalse(new MapVeinMaster(emptyToolsJson).Validate(out var emptyLogs));
            Assert.IsTrue(emptyLogs.Contains("noTools"));

            var badCountJson = JToken.Parse(@"{""mapObjects"":[],""mapVeins"":[
                {""veinGuid"":""33333333-0000-0000-0000-000000000004"",""veinName"":""badCount"",""veinType"":""item"",
                 ""veinParam"":{""itemGuid"":""00000000-0000-0000-1234-000000000001""},
                 ""outcropAddressablePath"":""Vanilla/Environment/StoneVein"",""soundEffectType"":""stone"",
                 ""handMiningType"":""minable"",
                 ""handMiningParam"":{""handMiningTools"":[{""toolItemGuid"":""00000000-0000-0000-1234-000000000001"",""attackSpeed"":1}],""minCount"":3,""maxCount"":1}}]}");
            Assert.IsFalse(new MapVeinMaster(badCountJson).Validate(out var countLogs));
            Assert.IsTrue(countLogs.Contains("badCount"));
        }
```

- [ ] **Step 2: テスト実行で失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinMasterTest"`
Expected: 追記分がFAIL（バリデーション未実装のためValidateがtrueを返す）

- [ ] **Step 3: MapVeinMasterUtil.Validateに検証を追加する**

`errorLogs += VeinParamGuidValidation();` の次行に `errorLogs += HandMiningValidation();` を追加し、`#region Internal` 内に:

```csharp
            string HandMiningValidation()
            {
                // fluid鉱脈のminable禁止・minable設定の内部整合を検証する
                // Validate that fluid veins are not minable and minable params are internally consistent
                var logs = "";
                foreach (var element in mapVeins)
                {
                    if (element.HandMiningParam is not MinableHandMiningParam minable) continue;

                    if (element.VeinParam is FluidVeinParam)
                        logs += $"[MapVeinMaster] Name:{element.VeinName} fluid veinはminableにできません\n";

                    if (minable.HandMiningTools.Length == 0)
                        logs += $"[MapVeinMaster] Name:{element.VeinName} handMiningToolsが空です\n";

                    foreach (var tool in minable.HandMiningTools)
                        if (MasterHolder.ItemMaster.GetItemIdOrNull(tool.ToolItemGuid) == null)
                            logs += $"[MapVeinMaster] Name:{element.VeinName} has invalid ToolItemGuid:{tool.ToolItemGuid}\n";

                    if (minable.MinCount < 1 || minable.MaxCount < minable.MinCount)
                        logs += $"[MapVeinMaster] Name:{element.VeinName} minCount/maxCountが不正です min:{minable.MinCount} max:{minable.MaxCount}\n";
                }

                return logs;
            }
```

- [ ] **Step 4: テスト実行して通ることを確認しコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinMasterTest"`
Expected: 全件PASS

```bash
git add moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests
git commit -m "feat: mapVeinsのhandMining整合バリデーションを追加（fluid×minable禁止等）"
```

---

### Task 6b: LoadMapObjectの欠損instanceIdスキップ（旧セーブ保護）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Map/MapObjectDatastore.cs:78-89`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapObjectDatastoreLoadTest.cs`（新規）

**Interfaces:**
- Consumes: 既存 `MapObjectJsonObject`（publicフィールド instanceId/isDestroyed/hp）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System.Collections.Generic;
using Game.Context;
using Game.SaveLoad.Json;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     マップから消えたmapObjectのセーブ状態が無効データとして安全にスキップされることを検証する
    ///     Verifies save states of mapObjects removed from the map are skipped safely as invalid data
    /// </summary>
    public class MapObjectDatastoreLoadTest
    {
        [Test]
        public void マップに存在しないinstanceIdのセーブは例外を出さずスキップされる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 実在しないinstanceId 999999のセーブエントリを混ぜてもロードが完走する
            // Loading completes even with a save entry for the non-existent instanceId 999999
            var saved = new List<MapObjectJsonObject>
            {
                new() { instanceId = 999999, isDestroyed = true, hp = 0 },
            };
            Assert.DoesNotThrow(() => ServerContext.MapObjectDatastore.LoadMapObject(saved));
        }
    }
}
```

（`MapObjectJsonObject`のフィールド名・初期化方法は実物 `Game.SaveLoad.Json` を確認して合わせる。コンストラクタ必須ならそれを使う）

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectDatastoreLoadTest"`
Expected: FAIL（KeyNotFoundException）

- [ ] **Step 3: LoadMapObjectをスキップ実装に変更する**

`MapObjectDatastore.cs` の `LoadMapObject` 内 throw を以下へ置換:

```csharp
            foreach (var savedMapObject in savedMapObjects)
            {
                // マップから消えたmapObjectのセーブ状態は無効データとして警告付きで捨てる（ホットバー無効割当と同型の裁定）
                // Save states of mapObjects removed from the map are dropped with a warning (same ruling as invalid hotbar slots)
                if (!_mapObjects.TryGetValue(savedMapObject.instanceId, out var loadedMapObject))
                {
                    Debug.LogWarning($"セーブ内のinstanceId:{savedMapObject.instanceId} のmapObjectがマップに存在しないためスキップします。");
                    continue;
                }

                // 破壊状況をロード
                // Load destruction status
                if (savedMapObject.isDestroyed) loadedMapObject.Destroy();
                if (savedMapObject.hp != loadedMapObject.CurrentHp) loadedMapObject.Attack(loadedMapObject.CurrentHp - savedMapObject.hp);
            }
```

- [ ] **Step 4: テスト実行PASS確認・コミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectDatastoreLoadTest"`
Expected: PASS

```bash
git add moorestech_server/Assets/Scripts/Game.Map moorestech_server/Assets/Scripts/Tests
git commit -m "feat: マップに存在しないmapObjectのセーブ状態をロード時スキップにする"
```

---

### Task 7: クライアント採掘FSMのIMiningTargetObject抽象化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningTargetObject.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningToolCandidate.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs`（IMiningTargetObject実装）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningController.cs`（フォーカス解決の2種マーカー対応）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningControllerContext.cs`（フォーカス対象をIMiningTargetObjectへ）
- Modify: FSM4状態（`MapObjectMiningIdleState.cs` / `MapObjectMiningFocusState.cs` / `MapObjectMiningMiningState.cs` / `MapObjectMiningMiningCompleteState.cs`）
- Test: 既存 `moorestech_client/Assets/Scripts/Client.Tests/Mining/MapObjectMiningAimTest.cs` / `MapObjectMiningEquipmentSwitchTest.cs` が緑のまま

**Interfaces:**
- Produces:

```csharp
// Client.Game/InGame/Mining/IMiningTargetObject.cs
using System.Collections.Generic;
using Client.Game.InGame.SoundEffect; // SoundEffectType所在は実装時に既存Complete stateのusingへ合わせる
using Core.Master;

namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     手掘りFSMが扱う採掘対象の抽象。mapObjectと露頭（vein）が実装する
    ///     Abstraction of a hand-mining target for the FSM; implemented by mapObjects and outcrops (veins)
    /// </summary>
    public interface IMiningTargetObject
    {
        // フォーカス同一性判定・距離判定に使う実体
        // The concrete object used for focus identity and distance checks
        UnityEngine.GameObject GameObject { get; }

        // 破壊済みmapObject等、もう掘れない状態か
        // Whether the target is exhausted (e.g., a destroyed mapObject)
        bool IsAvailable { get; }

        // PickUp（クリック1発）かツール採掘か
        // Whether this is one-click pickup or tool mining
        bool IsPickUp { get; }

        // 採掘に使えるツール一覧（ツールチップの推奨表示用）
        // Tools usable on this target (for the recommendation tooltip)
        List<ItemId> UsableToolItemIds { get; }

        // 装備中アイテムから使用ツールを解決する
        // Resolve the usable tool from the equipped item
        bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool);

        // 破壊音の種別（mapObjectはマスタのsoundEffectType、露頭はstone固定）
        // Destroy sound kind (mapObject: master soundEffectType, outcrop: fixed to stone)
        SoundEffectType DestroySoundType { get; }

        void SetFocused(bool focused);

        // 1振り分の攻撃をサーバへ送る（TargetType分岐は実装側が知る）
        // Send one swing to the server (implementations know their TargetType)
        void SendAttack();
    }
}

// Client.Game/InGame/Mining/MiningToolCandidate.cs
namespace Client.Game.InGame.Mining
{
    /// <summary>
    ///     採掘対象から解決した使用ツール。FSMはattackSpeedだけを使う（damageはサーバ専用）
    ///     A tool resolved from the target; the FSM uses only attackSpeed (damage is server-only)
    /// </summary>
    public readonly struct MiningToolCandidate
    {
        public readonly Core.Master.ItemId ToolItemId;
        public readonly float AttackSpeed;

        public MiningToolCandidate(Core.Master.ItemId toolItemId, float attackSpeed)
        {
            ToolItemId = toolItemId;
            AttackSpeed = attackSpeed;
        }
    }
}
```

- Consumes: `VanillaApiSendOnly.AttackMapObject`(Task 5)

**改修方針（各ファイル）:**

- `MapObjectGameObject.cs`: `IMiningTargetObject` を実装。`IsAvailable => !IsDestroyed`（既存の破壊状態フィールドに合わせる）、`IsPickUp` はマスタ`MiningType == PickUp`、`TryResolveUsableTool` は既存 `MapObjectMiningService.TryResolveUsableTool` を呼び `MiningToolCandidate(itemId, (float)tool.AttackSpeed)` へ詰め替え、`UsableToolItemIds` はマスタminingToolsのguid→ItemId列、`DestroySoundType` は既存Complete stateのマスタ`SoundEffectType`マッピングを移設、`SetFocused` は既存`OnFocus`を呼ぶ、`SendAttack` は `ClientContext.VanillaApi.SendOnly.AttackMapObject(InstanceId)`（Complete stateから移設）。
- `MapObjectMiningControllerContext.cs`: `CurrentFocusMapObjectGameObject` を `IMiningTargetObject CurrentFocusTarget` に改名・型変更。`SetFocusMapObjectGameObject` → `SetFocusTarget(IMiningTargetObject)`（変化時のみ `SetFocused(false/true)` プッシュの既存形を維持）。`ResolveUsableTool` はcontextから削除し対象interfaceへ移動。
- `MapObjectMiningController.cs`: `GetCurrentMapObject()` を `GetCurrentTarget()` に改め、レイキャストヒットで `MapObjectRayTarget` → その `MapObjectGameObject` を、無ければ `OutcropRayTarget`（Task 9で追加。本タスクでは `TryGetComponent<MapObjectRayTarget>` のみで良い）を解決して `IMiningTargetObject` を返す。
- FSM4状態: `MapObjectGameObject`・`MapObjectMasterElement`・`MiningMiningParam` への直接参照を排し、`IsPickUp`/`TryResolveUsableTool`/`UsableToolItemIds`/`DestroySoundType`/`SendAttack`/`IsAvailable` 経由に置換。進捗分母は `MiningToolCandidate.AttackSpeed`。装備変更中断（EquipmentSwitchTestの対象）は現行どおり `SelectedItem.Id` 比較で維持。

- [ ] **Step 1: interfaceとstructを新規作成する**（上記Producesのコード）
- [ ] **Step 2: MapObjectGameObjectにIMiningTargetObjectを実装する**
- [ ] **Step 3: Context→Controller→FSM4状態の順に参照を差し替える**（この間コンパイルは壊れるが、1コミットで完結させる）
- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 5: 既存クライアント採掘テスト2本が緑のままであることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectMiningAimTest|MapObjectMiningEquipmentSwitchTest"`
Expected: 全件PASS（抽象化は挙動不変）

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game
git commit -m "refactor: 採掘FSMのフォーカス対象をIMiningTargetObjectに抽象化しvein対応の受け口を作る"
```

**Note:** クラス名の `MapObjectMining*` → `Mining*` への改名（ファイル名+.metaのgit mv、シーン参照維持）は本タスクでは行わず、Task 13の後に余力があれば別コミットで実施する（改名はレビュー差分を膨らませ、シーン参照事故のリスクを本質変更と混ぜないため）。moores-code-reviewの命名レンズに指摘された場合はその時点で対応する。

---

### Task 8: （Task 5に統合済み・欠番）

クライアント送信APIはサーバのタグ変更と同一コンパイル単位のため、Task 5内で更新済み。

---

### Task 9: 露頭の実行時生成（OutcropGameObject / Datastore / 配線）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropRayTarget.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MapObjectMiningController.cs`（OutcropRayTarget解決を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（SerializeField+DI登録。mapObjectGameObjectDatastoreの前例に完全一致させる）
- Scene: MainGameStarterシーンへのGameObject追加と参照wiring（uloop execute-dynamic-code経由）

**Interfaces:**
- Consumes: `IMiningTargetObject`/`MiningToolCandidate`(Task 7) / `VanillaApiSendOnly.MineVein`(Task 5) / `InitialHandshakeResponse.MapLayout.MapVeins`(既存) / 生成型`MinableHandMiningParam`(Task 1)
- Produces: `OutcropGameObject.Initialize(MapVeinMasterElement element, Vector3Int minePosition)` / `OutcropRayTarget.OutcropGameObject`（コライダ側マーカー、`MapObjectRayTarget`と同型）

- [ ] **Step 1: OutcropRayTarget.csを作成する**（`MapObjectRayTarget.cs`と同型の薄いマーカー）

```csharp
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     露頭のコライダに付ける採掘レイキャスト用マーカー
    ///     Raycast marker attached to outcrop colliders for mining
    /// </summary>
    public class OutcropRayTarget : MonoBehaviour
    {
        public OutcropGameObject OutcropGameObject { get; private set; }

        public void Initialize(OutcropGameObject outcropGameObject)
        {
            OutcropGameObject = outcropGameObject;
        }
    }
}
```

- [ ] **Step 2: OutcropGameObject.csを作成する**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Mining;
using Core.Master;
using Mooresmaster.Model.MapModule;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     鉱脈の露頭。vein AABBごとに実行時生成される手掘りターゲット（サーバ非管理・純クライアント）
    ///     A vein outcrop; a hand-mining target instantiated per vein AABB (client-only, not server-managed)
    /// </summary>
    public class OutcropGameObject : MonoBehaviour, IMiningTargetObject
    {
        public GameObject GameObject => gameObject;
        public bool IsAvailable => true;
        public bool IsPickUp => false;

        public List<ItemId> UsableToolItemIds { get; } = new();

        private MinableHandMiningParam _minableParam;
        private Vector3Int _minePosition;

        public void Initialize(MapVeinMasterElement element, Vector3Int minePosition)
        {
            _minableParam = (MinableHandMiningParam)element.HandMiningParam;
            _minePosition = minePosition;

            // マスタのsoundEffectTypeをクライアント音種へ変換（既存Complete stateのmapObject用マッピングと同形）
            // Map master soundEffectType to the client sound kind (same shape as the mapObject mapping in Complete state)
            _destroySoundType = element.SoundEffectType == MapVeinMasterElement.SoundEffectTypeConst.tree
                ? SoundEffectType.DestroyTree
                : SoundEffectType.DestroyStone;

            // ツールチップ推奨表示用にツールItemIdを引いておく
            // Pre-resolve tool ItemIds for the recommendation tooltip
            foreach (var tool in _minableParam.HandMiningTools)
                UsableToolItemIds.Add(MasterHolder.ItemMaster.GetItemId(tool.ToolItemGuid));

            // 子コライダ全部にレイキャストマーカーを注入する（MapObjectGameObjectのInitializeと同型）
            // Inject raycast markers into all child colliders (same shape as MapObjectGameObject.Initialize)
            foreach (var childCollider in GetComponentsInChildren<Collider>())
            {
                var rayTarget = childCollider.gameObject.GetComponent<OutcropRayTarget>();
                if (rayTarget == null) rayTarget = childCollider.gameObject.AddComponent<OutcropRayTarget>();
                rayTarget.Initialize(this);
            }
        }

        public bool TryResolveUsableTool(ItemId equippedItemId, out MiningToolCandidate tool)
        {
            tool = default;
            if (equippedItemId == ItemMaster.EmptyItemId) return false;

            var equippedItemGuid = MasterHolder.ItemMaster.GetItemMaster(equippedItemId).ItemGuid;
            foreach (var handMiningTool in _minableParam.HandMiningTools)
            {
                if (handMiningTool.ToolItemGuid != equippedItemGuid) continue;
                tool = new MiningToolCandidate(equippedItemId, (float)handMiningTool.AttackSpeed);
                return true;
            }

            return false;
        }

        // 破壊音はマスタのsoundEffectType駆動（原木鉱脈=tree等。固定stoneだと1振りごとに誤った音が鳴る）
        // Destroy sound is master-driven via soundEffectType (e.g. log vein = tree); fixed stone would misplay every swing
        public SoundEffectType DestroySoundType => _destroySoundType;

        private SoundEffectType _destroySoundType;

        public void SetFocused(bool focused)
        {
            // 露頭には現状フォーカス演出が無い（アウトライン等は将来のアート課題）
            // Outcrops have no focus visuals yet (outline etc. is future art work)
        }

        public void SendAttack()
        {
            ClientContext.VanillaApi.SendOnly.MineVein(_minePosition);
        }
    }
}
```

（`SoundEffectType`のusing/名前空間はTask 7でDestroySoundTypeを移設した際の実名に合わせる）

- [ ] **Step 3: OutcropGameObjectDatastore.csを作成する**

`MapObjectGameObjectDatastore.cs`を前例として、次の仕様で実装する:

- MonoBehaviour。`[Inject] Construct(InitialHandshakeResponse handshakeResponse)` で `InstantiateOutcropsFromLayoutAsync().Forget()`
- `MapLayout.MapVeins` を走査し、各AABBについて:
  1. `MasterHolder.MapVeinMaster.GetElementOrNull(Guid.Parse(vein.VeinGuid))` でマスタ解決（null時LogError+skip）
  2. `element.HandMiningParam is MinableHandMiningParam` でない鉱脈（タングステン・fluid）はビジュアルのみ生成し `OutcropGameObject.Initialize` を呼ばない（コライダマーカー無し→レイキャスト対象外の純目印）。**この分岐のためInitialize呼び出し前にminable判定する**
  3. プレハブ解決: `AddressableLoader.LoadDefault<GameObject>(element.OutcropAddressablePath)`。guid単位で成功・失敗ともキャッシュ（MapObjectGameObjectDatastore.ResolvePrefabOrNullと同型）
  4. 位置: AABB中心XZ `center = (min + max + Vector3.one) * 0.5f`（maxは含む座標なので+1補正）の地表。地表Yは `Physics.Raycast(new Vector3(center.x, vein.MaxY + 50f, center.z), Vector3.down, out hit, 200f, LayerConst.Without_Player_MapObject_Block_LayerMask)` のhit点。非ヒット時は `vein.MaxY + 1` に置く（地形コライダ未ロード地点の保険。コメントで理由明記）
  5. `Instantiate(prefab, position, Quaternion.identity, transform)` → ルートに `OutcropGameObject` が無ければ `AddComponent`（プレハブ側に無くても動く）→ minableなら `Initialize(element, minePosition)`。`minePosition` は `Vector3Int.RoundToInt(AABB中心)` を min/max でクランプした値（サーバのGetOverVeinsに必ず内包される座標）
  6. 生成物のレイヤーを子孫含め `LayerConst.MapObjectLayer` に設定（採掘レイキャストのマスク対象に載せる）
  7. 100個ごとに `await UniTask.Yield()`（FrameYieldObjectInterval前例踏襲）
- vein数は千件規模（v8テンプレで1775件）なのでプレハブキャッシュ必須
- `IInitialEventApplyWaitTarget` は実装しない（露頭はイベント購読を持たない）

- [ ] **Step 4: MapObjectMiningControllerのフォーカス解決にOutcropRayTargetを追加する**

`GetCurrentTarget()`（Task 7）のヒット解決を:

```csharp
                if (hit.collider.gameObject.TryGetComponent(out MapObjectRayTarget mapObjectRayTarget))
                    return mapObjectRayTarget.MapObjectGameObject;
                if (hit.collider.gameObject.TryGetComponent(out OutcropRayTarget outcropRayTarget))
                    return outcropRayTarget.OutcropGameObject;
                return null;
```

の2段にする（距離・UI判定は既存のまま）。

- [ ] **Step 5: MainGameStarterへ配線する**

- `MainGameStarter.cs` の `mapObjectGameObjectDatastore` SerializeFieldの直下に `[SerializeField] private OutcropGameObjectDatastore outcropGameObjectDatastore;` を追加し、既存のDatastore登録行と同形式で登録する
- シーン編集はuloop execute-dynamic-codeで行う: MainGameStarterシーンを開き、mapObjectGameObjectDatastoreが付いているGameObjectを特定→同階層に `OutcropGameObjectDatastore` GameObjectを作成しコンポーネント追加→MainGameStarterのSerializeFieldへ `SerializedObject` で参照を設定→シーン保存

- [ ] **Step 6: コンパイル確認・コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add -A moorestech_client/Assets/Scripts moorestech_client/Assets/Scenes 2>/dev/null || git add -A moorestech_client/Assets
git commit -m "feat: vein AABBから露頭を実行時生成し採掘FSMのターゲットに載せる"
```

---

### Task 10: クライアントテスト（露頭ターゲット）

**Files:**
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/OutcropMiningTargetTest.cs`

**Interfaces:**
- Consumes: `OutcropGameObject.Initialize` / `IMiningTargetObject`(Task 7, 9)

- [ ] **Step 1: 失敗するテストを書く**

`MapObjectMiningAimTest.cs` のセットアップ形式（EditMode・GameObject手組み）を参考に、Addressablesを使わずコードで露頭を組み立てて検証する:

```csharp
using System;
using Client.Game.InGame.Map.Outcrop;
using Client.Game.InGame.Mining;
using Core.Master;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     露頭がIMiningTargetObjectとして正しくツール解決・マーカー注入することを検証する
    ///     Verifies outcrops resolve tools and inject ray markers correctly as IMiningTargetObject
    /// </summary>
    public class OutcropMiningTargetTest
    {
        // ForUnitTestマスタのIronVein（minable・tool 1234-0001・attackSpeed0.2）
        // ForUnitTest master's IronVein (minable, tool 1234-0001, attackSpeed 0.2)
        private static readonly Guid IronVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid UnmatchedToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 露頭はマスタのhandMiningToolsからツールを解決しコライダにマーカーを注入する()
        {
            // ForUnitTestマスタをロード（既存クライアントテストのマスタロード前例に従う）
            // Load the ForUnitTest master (following the existing client-test master-load precedent)
            // ※MapObjectMiningEquipmentSwitchTestのMasterHolder初期化手順をそのまま使う

            var outcropObject = new GameObject("outcrop");
            var colliderChild = new GameObject("collider");
            colliderChild.transform.SetParent(outcropObject.transform);
            colliderChild.AddComponent<BoxCollider>();

            var outcrop = outcropObject.AddComponent<OutcropGameObject>();
            var element = MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid);
            outcrop.Initialize(element, new Vector3Int(0, 5, 0));

            // コライダ子にOutcropRayTargetが注入されている
            // The collider child received an OutcropRayTarget
            Assert.IsNotNull(colliderChild.GetComponent<OutcropRayTarget>());
            Assert.AreEqual(outcrop, colliderChild.GetComponent<OutcropRayTarget>().OutcropGameObject);

            // 対応ツールは解決でき、attackSpeedがマスタ値になる
            // The matching tool resolves with the master attackSpeed
            var toolItemId = MasterHolder.ItemMaster.GetItemId(ToolItemGuid);
            Assert.IsTrue(outcrop.TryResolveUsableTool(toolItemId, out var tool));
            Assert.AreEqual(0.2f, tool.AttackSpeed, 0.0001f);

            // 非対応ツールと素手は解決できない
            // Non-matching tools and bare hands do not resolve
            Assert.IsFalse(outcrop.TryResolveUsableTool(MasterHolder.ItemMaster.GetItemId(UnmatchedToolItemGuid), out _));
            Assert.IsFalse(outcrop.TryResolveUsableTool(ItemMaster.EmptyItemId, out _));

            UnityEngine.Object.DestroyImmediate(outcropObject);
        }
    }
}
```

（MasterHolderの初期化は既存 `MapObjectMiningEquipmentSwitchTest.cs` が行っている方法＝`MoorestechServerDIContainerGenerator`でForUnitTestマスタを立てる手順を冒頭にコピーする。コメント部を実装時に具体化する）

- [ ] **Step 2: テスト実行で失敗→実装調整→PASS確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "OutcropMiningTargetTest"`
Expected: PASS（Task 9実装が正しければ一発で通る。失敗したらTask 9側を修正）

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests
git commit -m "test: 露頭のツール解決とレイキャストマーカー注入のテストを追加"
```

---

### Task 11: 露頭プレハブ整備とv8アドレス整合

**Files:**
- Modify（uloop EDC経由）: `moorestech_client/Assets/AddressableResources/Environment/` — `StoneVein` / `LogVein` / `IronVein` / `CoalVein` の既存4プレハブ + 新規複製7個（`CopperVein` / `TungstenVein` / `ClayVein` / `BronzeVein` / `PebbleVein` / `WaterVein` / `OilVein`）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`（outcropAddressablePath更新）

**前提知識:** AddressableResources配下はSmartAddresserがパスから自動採番する（例: `AddressableResources/Environment/StoneVein.prefab` → アドレス `Vanilla/Environment/StoneVein`）。.metaは手動作成禁止、プレハブ操作は必ずuloop execute-dynamic-code（Unityがシリアライズする正規ルート）。

- [ ] **Step 1: uloop EDCで既存4プレハブを露頭仕様に整える**

各プレハブ（StoneVein/LogVein/IronVein/CoalVein）についてEDCスクリプトで:
1. `PrefabUtility.LoadPrefabContents` でロード
2. ルートに `OutcropGameObject` が無ければ `AddComponent`
3. `MapObjectGameObject` / `MapObjectRayTarget` / HPバー関連コンポーネントが付いていれば `DestroyImmediate`（露頭はmapObjectではない。コライダ自体は残す）
4. コライダが1つも無ければルートに `BoxCollider` を追加（サイズはレンダラーのboundsに合わせる）
5. ルート含む全階層のlayerを `MapObject` に設定
6. `PrefabUtility.SaveAsPrefabAsset` → `UnloadPrefabContents`

- [ ] **Step 2: uloop EDCで不足7種を複製生成する**

`StoneVein.prefab` を `AssetDatabase.CopyAsset` で `CopperVein.prefab` 等7個に複製（見た目は当面プレースホルダ。アート差し替えは別途）。複製後Step 1と同じ整形を適用。

- [ ] **Step 3: v8のoutcropAddressablePathを実アドレスに更新する**

`../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json` のmapVeins:

| veinName | outcropAddressablePath |
|---|---|
| 石鉱脈 | Vanilla/Environment/StoneVein |
| 原木鉱脈 | Vanilla/Environment/LogVein |
| 鉄鉱石鉱脈 | Vanilla/Environment/IronVein |
| 石炭鉱脈 | Vanilla/Environment/CoalVein |
| 銅の鉱石鉱脈 | Vanilla/Environment/CopperVein |
| タングステン鉱石鉱脈 | Vanilla/Environment/TungstenVein |
| 粘土鉱脈 | Vanilla/Environment/ClayVein |
| 青銅の鉱石鉱脈 | Vanilla/Environment/BronzeVein |
| 小石鉱脈 | Vanilla/Environment/PebbleVein |
| 水鉱脈 | Vanilla/Environment/WaterVein |
| 原油鉱脈 | Vanilla/Environment/OilVein |

- [ ] **Step 4: コンパイル・コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add -A moorestech_client/Assets/AddressableResources
git commit -m "feat: 露頭プレハブを整備（既存4整形+7種プレースホルダ複製）"
cd ../moorestech_master && git add server_v8 && git commit -m "feat: v8のoutcropAddressablePathを実プレハブアドレスへ更新" && cd -
```

---

### Task 12: v8マスタ移行（鉱脈mapObject削除・チャレンジ対応）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`（mapObjects 4件削除）
- Modify: `../moorestech_master/server_v8/map/map.json`（該当インスタンス約100件削除）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（mapObjectPin 2件・**ユーザー裁定に従う**）

- [ ] **Step 1: 鉱脈mapObject 4種をマスタとワールドから削除するスクリプトを実行する**

```python
import json
master_path = '../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json'
world_path = '../moorestech_master/server_v8/map/map.json'

master = json.load(open(master_path))
vein_object_guids = {o['mapObjectGuid'] for o in master['mapObjects'] if o['mapObjectName'] in ('原木鉱脈', '石鉱脈', '鉄鉱脈', '石炭鉱脈')}
master['mapObjects'] = [o for o in master['mapObjects'] if o['mapObjectGuid'] not in vein_object_guids]
json.dump(master, open(master_path, 'w'), ensure_ascii=False, indent=4)

world = json.load(open(world_path))
before = len(world['mapObjects'])
world['mapObjects'] = [o for o in world['mapObjects'] if o['mapObjectGuid'] not in vein_object_guids]
print(f'removed {before - len(world["mapObjects"])} instances')  # 期待値: 100
json.dump(world, open(world_path, 'w'), ensure_ascii=False, indent=4)
```

- [ ] **Step 2: challenges.jsonのmapObjectPin 2件をveinPinへ差し替える（裁定確定 2026-08-04）**

対象: 「石を採掘する」(ba99109e-…)・「砕いた石材を25個備蓄する」(48386dbb-…) の `tutorials` 内 `mapObjectPin`（石鉱脈mapObject guid参照）。Task 12bで新設する `veinPin` へ差し替える:

```json
  {
   "tutorialType": "veinPin",
   "tutorialParam": {
    "veinGuid": "<石鉱脈veinのguid（map.json masterのveinName=石鉱脈から読む。手打ちしない）>",
    "pinText": "石鉱脈から石を採掘"
   }
  }
```

- [ ] **Step 3: 旧guid残存ゼロを確認する**

Run: `grep -rn "684eb2c1-d1c4\|d133b579-3c8c\|356cc324-0e9a\|39ba8217-0852" ../moorestech_master/server_v8/`
Expected: ヒット0件

- [ ] **Step 4: v8マスタでサーバがロードできることを確認しコミット**

CliConvertTest等のマスタロード系テスト＋起動確認（プレイテストDSLがあればsmoke、無ければテストのみ）:

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CliConvertTest|GenerationMasterTest|MapVeinMasterTest"`
Expected: 全件PASS

```bash
cd ../moorestech_master && git add server_v8 && git commit -m "feat: 巨大HP鉱脈mapObject4種を削除しvein手掘りへ一本化" && cd -
```

---

### Task 12b: veinPinチュートリアルの新設（誘導ピンのvein対応）

**Files:**
- Modify: `VanillaSchema/challenges.yml`（tutorialType options + tutorialParam switch cases）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs`（veinGuid索引+最寄り露頭検索を追加）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/VeinPin.cs`（`MapObjectPin.cs`と同型）
- Modify: tutorialType dispatch箇所（`grep -rn "mapObjectPin\|MapObjectPin" moorestech_client/Assets/Scripts --include="*.cs"` で特定されるチュートリアル起動switch）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json`（Task 12 Step 2の差し替えと同時にコミット）

**Interfaces:**
- Consumes: `OutcropGameObject`(Task 9)
- Produces: 生成型 `VeinPinTutorialParam`（`.VeinGuid`）/ `OutcropGameObjectDatastore.SearchNearestOutcrop(Guid veinGuid, Vector3 position): OutcropGameObject`（`MapObjectGameObjectDatastore.SearchNearestMapObject`と同型の線形探索）

- [ ] **Step 1: challenges.ymlのtutorialTypeに`veinPin`を追加し、tutorialParamのcasesへ追記する**

`tutorialType` の `options` に `- veinPin` を追加し、`tutorialParam` switchに:

```yaml
              - when: veinPin
                type: object
                properties:
                - key: veinGuid
                  type: uuid
                  foreignKey:
                    schemaId: map
                    foreignKeyIdPath: /mapVeins/[*]/veinGuid
                    displayElementPath: /mapVeins/[*]/veinName
                - key: pinText
                  type: string
                  default: pin text
```

（mapObjectPinの既存case（foreignKey→/mapObjects/[*]/mapObjectGuid）と同型。_CompileRequesterのdummyTextも変更）

- [ ] **Step 2: OutcropGameObjectDatastoreにveinGuid索引と最寄り検索を追加する**

生成時に `Dictionary<Guid, List<OutcropGameObject>>` へ登録し、`SearchNearestMapObject`（`MapObjectGameObjectDatastore.cs:168-187`）と同型の `SearchNearestOutcrop(Guid veinGuid, Vector3 position)` を追加する。

- [ ] **Step 3: VeinPin.csを作成しdispatchへ配線する**

`MapObjectPin.cs`（`Client.Game/InGame/Tutorial/`）を読み、同一構造で `VeinPin.cs` を作成（対象解決だけ `SearchNearestOutcrop` に差し替え）。tutorialTypeのdispatch switch（grepで特定）に `veinPin` ケースを追加する。未知tutorialTypeの既存ハンドリング（例外 or 無視）は現行方針に合わせる。

- [ ] **Step 4: コンパイル・チュートリアル関連テスト確認・コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Challenge|Tutorial"`
Expected: 既存全件PASS

```bash
git add VanillaSchema/challenges.yml moorestech_server/Assets/Scripts/Core.Master moorestech_client/Assets/Scripts/Client.Game
git commit -m "feat: veinPinチュートリアルを新設し誘導ピンが最寄りの露頭を指せるようにする"
```

---

### Task 13: 統合検証

- [ ] **Step 1: フルコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0・警告増加なし

- [ ] **Step 2: 採掘・マスタ・マップ関連の全テスト**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Mining|MapVein|MapObject|CliConvert|GetMapData"`
Expected: 全件PASS

- [ ] **Step 3: EditModeInPlayingTestスイートを実行する**

Task 1でEditModeInPlayingTestModのmap.jsonを更新しているのに対応する実行関門。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EditModeInPlayingTest"`
Expected: 既存全件PASS（新フィールド追記でローダーが落ちないこと）。ドメインリロードエラーが出たら45秒待ってリトライ

- [ ] **Step 4: プレイテストDSLでsmoke確認（必須・スキップ不可）**

unity-playmode-recorded-playtestスキルのDSLで「石の斧をホットバー装備→石鉱脈の露頭付近へWarp→露頭を叩く→インベントリに石が入る」シナリオを実行し録画確認する。本ブランチのmasterピンはライブの`../moorestech_master`そのもの（Task 1/11/12で同期更新済み）であり非互換は発生しないため、スキップ理由は存在しない。このステップが露頭プレハブ11種のAddressable実解決・uloop EDCシーン配線・OutcropGameObjectDatastoreの起動挙動・va:mining往復を検証する唯一の関門（Task 10はGameObject手組みでAddressables/Datastoreを迂回している）。

- [ ] **Step 5: 全作業コミット確認**

Run: `git status`（本repoと../moorestech_master両方）
Expected: クリーン（未コミット残なし）

（最終報告に一言添える: mooreseditor側のスキーマ追随は本planのスコープ外。新フィールドをエディタで編集する際は旧プラグインキャッシュ+新データで白い空箱ノード化する既知の罠があるためアプリ再起動が必要）

---

### Task 14: moores-code-review（必須・最終）

- [ ] 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の機械的修正を適用し、設計判断はAskUserQuestionで裁定を仰ぐ。

---

## 判断記録（ADR）

- 設計の正: `docs/adr/0007-vein-as-hand-minable-target.md`（+ `.decisions/2026-08-04-*.md` 裁定10件）
- **handMiningToolsキー名（planning判断）**: mapObjects側の生成クラス`MiningToolsElement`との同名衝突を避けるため`miningTools`ではなく`handMiningTools`とした。出所: agent前提（SourceGeneratorのキー由来クラス名生成という技術制約）
- **クールダウン抽出先はMiningCooldownService**: MapObjectMiningServiceに残すとvein側から逆参照になるため独立サービス化。出所: agent前提（ADR-0007「サービス共有」の実装形）
- **露頭Datastoreの配置はMapObjectGameObjectDatastore前例に完全一致**（Mono+シーン配置+[Inject] Construct+フレーム分散+プレハブキャッシュ）。出所: agent前提（前例一致原則）
- **FSMクラス改名（MapObjectMining*→Mining*）は本plan本体から除外**し、機能完成後の別コミット候補とした。シーン参照事故リスクを本質変更と分離するため。出所: agent前提
- **露頭プレハブは既存4流用+7プレースホルダ複製**。アート制作は本planのスコープ外。出所: agent前提
- **タングステン・fluid鉱脈の露頭はコライダマーカー無しの純目印**として生成（minable判定でInitialize分岐）。出所: ADR-0007「fluid露頭は叩けない純ビジュアル」の実装形
- **チャレンジmapObjectPin 2件（石鉱脈参照）の扱い**: 要裁定 → plan承認時のAskUserQuestionで確定しTask 12 Step 2に反映する
- **mapVeinsにsoundEffectType追加（simulator review適用）**: 露頭の破壊音を固定stoneにせずマスタ駆動へ。原木鉱脈=treeの引き継ぎ。出所: シミュレーター予測（deviation-cases §2「値が当面固定でも置き場はマスタ」）→ ユーザー承認待ち 2026-08-04
- **Task 13のプレイテストsmoke必須化＋EditModeInPlayingTest実行追加（simulator review適用）**: masterピンはライブ../moorestech_masterでスキップ理由が存在せず、露頭のAddressable解決・シーン配線・va:mining往復を検証する唯一の関門のため。出所: シミュレーター予測（検証カバレッジレンズ）→ ユーザー承認待ち 2026-08-04
- **旧セーブの欠損mapObject instanceIdはロード時スキップ（Task 6b）**: 出所: シミュレーター予測→ユーザー承認 2026-08-04（.decisions/2026-08-04-セーブの欠損mapObjectはロード時スキップにする.md）
- **チャレンジ誘導はveinPinチュートリアル新設（Task 12b）**: 出所: シミュレーター予測→ユーザー承認 2026-08-04（.decisions/2026-08-04-チャレンジ誘導はveinPinチュートリアルを新設する.md）
