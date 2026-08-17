# MapMaking Visual Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** generatedワールドの木・岩・草の見た目（樹種・スケール・回転・草分布）を MapMaking プロジェクト（`TmpUnityPjt/MapMaking`）のバイオームプリセットと同一にする。

**Architecture:** 3系統の独立した改修。(A) 生成パイプラインが計算済みのTransform（回転・スケール・sink）を `PlacedMapObject`→`map.json`→`va:mapData`→クライアントInstantiateの全区間に貫通させる。(B) MapMakingプリセットが参照するBK Pure Nature樹種・岩を個別mapObjectとしてスクリプト一括登録（master生成・ラッパープレハブ生成・generation.json同期）。(C) クライアントのdetail生成に木・岩の距離場を供給し、休眠中のtreeDistanceFilter/objectDistanceFilterを有効化する。

**Tech Stack:** Unity 6000.3.8f1 / URP 17.3.0 / uloop CLI / mooresmaster SourceGenerator / Python 3（データ生成スクリプト）/ NUnit

## Requirements

- R1: 生成パイプラインの回転・スケールがクライアント表示に反映される（受け入れ: generatedワールドで木が個体ごとに異なる向き・サイズで表示される）
- R2: sinkはY座標へ畳み込み、bendFactorは破棄する（受け入れ: `PlacedMapObject`にSink/BendFactorフィールドが存在しない）
- R3: 配置データはTransform相当の3要素（位置Vector3・回転・スケールVector3）を`map.json`・`va:mapData`・クライアントの全区間で持つ（受け入れ: MapInfoJsonBuilderTest/GetMapDataProtocolTestが新フィールドを検証して通る）
- R4: template map.json（手作り2002件）は形式移行のみ行い見た目は現状維持（受け入れ: 移行後のtemplateワールドが従来どおり起動しエラー0）
- R5: MapMakingの有効バイオーム（Forest/Grassland/Savanna/Mesa）の全有効樹種・岩をmapObjectとして登録する。disabledエントリ（Desert Olivebush・Mesa 3種・Savanna Bush）は登録対象から除外しない（樹種登録は全樹種）が、treePlacementへは載せない
- R6: Jungle/Woodsは旧スキーマプリセットの樹種リスト（Kapokier/Banana/Tropica/Musa、PineTree/BirchTree）を移植する。バイオーム自体は無効のまま
- R7: 新規mapObjectのhp・ドロップ・採掘設定は既存値の複製（木=既存「木」と同値、岩=石ドロップのMining、小型PebbleのみPickUp）
- R8: generation.jsonの各バイオームtreePlacementがMapMakingプリセットと同一のパラメータ・樹種構成になる（受け入れ: forest prototype 0の再生成結果が既存portと一致 ※mapObjects guid以外）
- R9: 草のtreeDistanceFilter/objectDistanceFilterが機能する（受け入れ: TerrainDetailBuilderが距離場を渡し、単体テストで距離フィルタが密度に影響する）
- R10: 死にフラグ`generateDetail`/`generateTexture`をスキーマ・マスタ・コードから削除する（`generateObject`は配線済みのため残す）
- R11: 最終検収として generatedワールドのスクショと MapMaking のスクショを外部監査で突き合わせる
- やらないこと: 描画環境（RPアセット/Volume/風）の同期、既存「木」(Birch)/「ブッシュ」の見た目変更、templateマップの見た目変更、Bush.prefabのRayTarget欠落修正（別タスク）、5x5タイル化、objectConfig（クラスタ配置）の有効化

## Global Constraints

- 作業ブランチ: `feat/mapmaking-visual-parity`（origin/master b5644e673 起点）。SDDはworktree隔離必須（`.decisions/2026-08-13-SDDはworktree隔離を必須ゲートにする.md`）
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（サーバーコードもクライアントプロジェクトからコンパイルされる）
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- partial禁止・`Func<>`禁止・try-catch原則禁止・1ファイル200行以下・1ディレクトリ10コードファイルまで・デフォルト引数禁止
- コメントは日本語・英語の2行セット（各1行）
- optionalフォールバック禁止: 新フィールドは必須として全JSON一括更新（ADR-0010）
- .metaファイル手動作成禁止。プレハブの生成・編集はEditorスクリプト/`uloop execute-dynamic-code`経由のみ（テキスト直編集禁止）
- masterデータ変更は `../moorestech_master`（現在 detached @ 56dbd35）にブランチを切ってコミットし、`.moorestech-external-revisions.json` のピンを更新する
- MapMakingプロジェクト（`TmpUnityPjt/MapMaking`）とBKアセット（`moorestech_client/Assets/PersonalAssets/moorestech-client-private/BK/`）は読み取り専用。一切変更しない
- 新規Pythonスクリプトは `scripts/mapmaking-parity/` に置く（再実行可能・冪等に作る）

---

## File Structure

```
[Phase A: Transform貫通]
Modify: moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationOutput.cs        … PlacedMapObjectへRotation/Scale追加
Modify: moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs           … AppendMapObjectsでsink畳み込み+転記
Modify: moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs                    … rotX..scaleZ 6フィールド追加
Modify: moorestech_server/Assets/Scripts/Game.MapGeneration/Export/MapInfoJsonBuilder.cs           … 新フィールド書き出し
Modify: moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/MapObjectLayoutMessagePack.cs … Key(5)-(10)追加
Modify: moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs      … 新フィールド送信
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs … 回転・スケール適用
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs   … HPバーの逆スケール補正
Modify: moorestech_client/Assets/Scripts/Editor/MapAuthoring/MapAuthoringExporter.cs               … 実Transformを書き出し
Modify: moorestech_client/Assets/Scripts/Editor/MapAuthoring/MapAuthoringImporter.cs               … import時にTransform適用
Modify: moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapInfoJsonBuilderTest.cs
Modify: moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs
Create: scripts/mapmaking-parity/migrate_template_map.py                                           … template map.json形式移行
Modify: ../moorestech_master/server_v8/map/map.json ほかテスト用map.json 3件                        … 移行実行結果

[Phase B: 樹種・岩登録]
Create: scripts/mapmaking-parity/extract_mapmaking_species.py   … MapMakingプリセット→species-inventory.json
Create: scripts/mapmaking-parity/species-inventory.json         … 抽出結果（コミットする・後続3スクリプトの入力）
Create: scripts/mapmaking-parity/gen_map_master.py              … master map.jsonへmapObjects追記
Create: scripts/mapmaking-parity/gen_generation_treeplacement.py … generation.jsonのtreePlacement再生成
Create: moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/MapObjectWrapperGeneratorMenu.cs
Create: moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs
Create: moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperAddressableRegistrar.cs
Create: moorestech_client/Assets/AddressableResources/Environment/Tree/<Pack>/<Name>.prefab（生成物 約67個）
Create: moorestech_client/Assets/AddressableResources/Environment/Rock/<Pack>/<Name>.prefab（生成物 約27個）
Create: moorestech_client/Assets/Scripts/Client.Tests/EditModeTest/MapObjectAddressableLoadTest.cs  … 全mapObjectアドレスのロード検証
Modify: ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json
Modify: ../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json
Modify: .moorestech-external-revisions.json                                                        … masterピン更新

[Phase C: 草距離場 + 死にフラグ削除]
Create: moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Detail/DetailDistanceFieldBuilder.cs
Create: moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Detail/DetailDistanceRadius.cs
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDetailBuilder.cs
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs（距離場入力の受け渡し）
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs（MapLayoutの受け渡し）
Modify: moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Cache/TerrainVisualCacheFormat.cs（version 2→3）
Create: moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/DetailDistanceFieldBuilderTest.cs
Modify: VanillaSchema/generation.yml（generateDetail/generateTexture削除）
Modify: moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Terrain/TerrainGenerationConfig.cs
Modify: moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/GenerationRuntimeConfigFactory.cs
Modify: ../moorestech_master/.../master/generation.json（キー削除）
```

各タスクの `Interfaces` に記載のシグネチャが後続タスクの前提。タスクは番号順に実施する（Phase間は A→B→C の依存: Bの検証はAのTransform表示を前提、Cの距離場はBの配置データを使う）。

---

### Task 1: サーバー生成出力にTransformを乗せる

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/MapGenerationOutput.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/MapInfoJsonBuilder.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapInfoJsonBuilderTest.cs`

**Interfaces:**
- Consumes: `PlacementEntry`（既存。`Rotation: Quaternion` / `Scale: Vector3` / `Sink: float` を既に持つ）
- Produces: `PlacedMapObject { string MapObjectGuid; Vector3 Position; Quaternion Rotation; Vector3 Scale; }`／`MapObjectInfoJson` に `RotX/RotY/RotZ/ScaleX/ScaleY/ScaleZ`（floatフィールド、JSONキー `rotX`..`scaleZ`）と `[JsonIgnore] Quaternion Rotation`・`[JsonIgnore] Vector3 Scale` 導出プロパティ

- [ ] **Step 1: 失敗するテストを書く** — `MapInfoJsonBuilderTest.cs` に追加:

```csharp
[Test]
public void 回転スケールとsink畳み込みがmapObjectsへ出力される()
{
    var output = new MapGenerationOutput
    {
        MapObjects = new List<PlacedMapObject>
        {
            new()
            {
                MapObjectGuid = "6a53fef8-2cf5-41fe-9922-21fd7dd4ab6c",
                Position = new Vector3(10f, 5f, 20f),
                Rotation = Quaternion.Euler(0f, 90f, 0f),
                Scale = new Vector3(0.5f, 1.5f, 0.5f),
            },
        },
    };

    var json = MapInfoJsonBuilder.Build(output);

    var mapObject = json.MapObjects[0];
    Assert.AreEqual(90f, mapObject.RotY, 0.001f);
    Assert.AreEqual(0.5f, mapObject.ScaleX, 0.001f);
    Assert.AreEqual(1.5f, mapObject.ScaleY, 0.001f);
    Assert.AreEqual(90f, mapObject.Rotation.eulerAngles.y, 0.001f);
}
```

既存テストの `new PlacedMapObject { ... }` 初期化子には `Rotation = Quaternion.identity, Scale = Vector3.one` を追記して回す（コンパイルを通すため。値未指定だとScaleがゼロになる点に注意）。

- [ ] **Step 2: コンパイルして失敗を確認** — Run: `uloop compile --project-path ./moorestech_client` → Expected: `PlacedMapObject` に `Rotation` が無い旨のコンパイルエラー

- [ ] **Step 3: `MapGenerationOutput.cs` の `PlacedMapObject` を拡張**

```csharp
    public class PlacedMapObject
    {
        public string MapObjectGuid;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }
```

- [ ] **Step 4: `VanillaGenerator.cs` の `AppendMapObjects` でsinkを畳み込みTransformを転記**

```csharp
        static void AppendMapObjects(List<PlacedMapObject> target, List<PlacementEntry> entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.MapObjectGuid)) continue;

                // sinkはここでY座標へ畳み込み、以降の区間はTransform3要素だけを運ぶ（ADR-0010）
                // Sink folds into Y here; every later stage carries only the transform triple (ADR-0010)
                target.Add(new PlacedMapObject
                {
                    MapObjectGuid = e.MapObjectGuid,
                    Position = e.WorldPosition - new Vector3(0f, e.Sink, 0f),
                    Rotation = e.Rotation,
                    Scale = e.Scale,
                });
            }
        }
```

- [ ] **Step 5: `MapInfoJson.cs` の `MapObjectInfoJson` へ6フィールド追加**（既存のflatスタイル踏襲）

```csharp
    public class MapObjectInfoJson
    {
        [JsonProperty("instanceId")] public int InstanceId;
        [JsonProperty("mapObjectGuid")] public string MapObjectGuidStr;
        [JsonIgnore] public Guid MapObjectGuid => new(MapObjectGuidStr);

        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("z")] public float Z;

        // 回転はオイラー角(度)・スケールは3軸。Transformと同じ3要素を運ぶ（ADR-0010）
        // Rotation as euler degrees, scale on all axes: the same triple a Transform holds (ADR-0010)
        // Required.Always: 未移行データの欠損キーが黙ってScale=0になる無言破壊をロード時に即例外へ変える
        // Required.Always turns a missing key in unmigrated data into a load-time failure instead of a silent Scale=0
        [JsonProperty("rotX", Required = Required.Always)] public float RotX;
        [JsonProperty("rotY", Required = Required.Always)] public float RotY;
        [JsonProperty("rotZ", Required = Required.Always)] public float RotZ;
        [JsonProperty("scaleX", Required = Required.Always)] public float ScaleX;
        [JsonProperty("scaleY", Required = Required.Always)] public float ScaleY;
        [JsonProperty("scaleZ", Required = Required.Always)] public float ScaleZ;

        [JsonIgnore] public Vector3 Position => new(X, Y, Z);
        [JsonIgnore] public Quaternion Rotation => Quaternion.Euler(RotX, RotY, RotZ);
        [JsonIgnore] public Vector3 Scale => new(ScaleX, ScaleY, ScaleZ);
    }
```

- [ ] **Step 6: `MapInfoJsonBuilder.cs` の `BuildMapObjects` で書き出し**

```csharp
                    var euler = placed.Rotation.eulerAngles;
                    mapObjects.Add(new MapObjectInfoJson
                    {
                        InstanceId = i,
                        MapObjectGuidStr = placed.MapObjectGuid,
                        X = placed.Position.x,
                        Y = placed.Position.y,
                        Z = placed.Position.z,
                        RotX = euler.x,
                        RotY = euler.y,
                        RotZ = euler.z,
                        ScaleX = placed.Scale.x,
                        ScaleY = placed.Scale.y,
                        ScaleZ = placed.Scale.z,
                    });
```

- [ ] **Step 7: コンパイル** — Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0。`MapObjectInfoJson` を構築している他の箇所（`MapAuthoringExporter.cs` はTask 4で対応、それ以外があればこのタスクで identity/one を設定）をコンパイルエラーで洗い出して対応する
- [ ] **Step 8: テスト実行** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapInfoJsonBuilderTest"` → Expected: PASS
- [ ] **Step 9: コミット** — `git add`（変更ファイル）`git commit -m "feat(server): mapObject配置にTransform3要素を追加しsinkをY畳み込み"`

---

### Task 2: template/テスト用 map.json の形式移行

**Files:**
- Create: `scripts/mapmaking-parity/migrate_template_map.py`
- Modify: `../moorestech_master/server_v8/map/map.json`（スクリプト実行結果）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/map/map.json`（同上）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ConfigOnly/map/map.json`（同上）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/map/map.json`（同上・379件。`EditModeInPlayingTestUtil.cs:31/83`が同じMapInfoJsonローダーで読むため、漏らすとScale=(0,0,0)の無言破壊になる）

**Interfaces:**
- Consumes: Task 1 のJSONフィールド名（`rotX/rotY/rotZ/scaleX/scaleY/scaleZ`）
- Produces: 対象4ファイルの全mapObjectsに rotation=0/scale=1 が付与されたmap.json

- [ ] **Step 1: 移行スクリプトを書く**

```python
#!/usr/bin/env python3
"""template/テスト用map.jsonへTransform3要素を付与する形式移行（冪等）。

見た目は現状維持のため rotation=0 / scale=1 の identity を全件に与える。
裁定: .decisions/2026-08-16-templateマップは形式移行のみで見た目現状維持.md
"""
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
TARGETS = [
    REPO.parent / "moorestech_master/server_v8/map/map.json",
    REPO / "moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/map/map.json",
    REPO / "moorestech_server/Assets/Scripts/Tests.Module/TestMod/ConfigOnly/map/map.json",
    REPO / "moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/map/map.json",
]

def migrate(target: Path) -> None:
    data = json.loads(target.read_text(encoding="utf-8"))
    for map_object in data["mapObjects"]:
        map_object.setdefault("rotX", 0.0)
        map_object.setdefault("rotY", 0.0)
        map_object.setdefault("rotZ", 0.0)
        map_object.setdefault("scaleX", 1.0)
        map_object.setdefault("scaleY", 1.0)
        map_object.setdefault("scaleZ", 1.0)
    target.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{target.name}: migrated {len(data['mapObjects'])} mapObjects ({target})")

def main() -> None:
    for target in TARGETS:
        migrate(target)

if __name__ == "__main__":
    main()
```

- [ ] **Step 2: masterリポジトリにブランチを切る** — Run: `git -C ../moorestech_master checkout -b feat/mapmaking-visual-parity`
- [ ] **Step 3: 実行と検証** — Run: `python3 scripts/mapmaking-parity/migrate_template_map.py` → Expected: 4ファイル分の `migrated N mapObjects`（master template 2002件・EditModeInPlayingTest 379件）。`python3 -c "import json;d=json.load(open('../moorestech_master/server_v8/map/map.json'));assert all('scaleY' in o for o in d['mapObjects'])"` がエラーなし
- [ ] **Step 4: コミット** — moorestech側: `git add scripts/mapmaking-parity/migrate_template_map.py moorestech_server/Assets/Scripts/Tests.Module && git commit -m "feat: map.jsonのTransform形式移行スクリプトとテストデータ移行"`。master側: `git -C ../moorestech_master add server_v8/map/map.json && git -C ../moorestech_master commit -m "feat: mapObjectsへTransform3要素を付与(identity)"`

---

### Task 3: プロトコル `va:mapData` にTransformを乗せる

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/MapObjectLayoutMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs:56-58`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`

**Interfaces:**
- Consumes: `MapObjectInfoJson.RotX..ScaleZ`（Task 1）
- Produces: `MapObjectLayoutMessagePack` に `[Key(5)] float RotX` `[Key(6)] float RotY` `[Key(7)] float RotZ` `[Key(8)] float ScaleX` `[Key(9)] float ScaleY` `[Key(10)] float ScaleZ`、コンストラクタは `(int instanceId, string mapObjectGuid, float x, float y, float z, float rotX, float rotY, float rotZ, float scaleX, float scaleY, float scaleZ)`

- [ ] **Step 1: 失敗するテストを書く** — テストデータ `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/map/map.json` の instanceId=3 のエントリ（guid `00000000-0000-1111-0000-000000000001`、x=1/z=1。Task 2でidentity付与済み）を `"rotY": 90.0, "scaleX": 0.5, "scaleY": 1.5, "scaleZ": 0.5` に書き換え、`GetMapDataProtocolTest.GetMapDataLayoutTest` の `object3` 検証ブロックへ追加:

```csharp
            Assert.AreEqual(90f, object3.RotY);
            Assert.AreEqual(0.5f, object3.ScaleX);
            Assert.AreEqual(1.5f, object3.ScaleY);
            Assert.AreEqual(0.5f, object3.ScaleZ);

            // identity移行された既存エントリはrot0/scale1で届くことを対照確認する
            // Migrated identity entries must arrive as rot 0 / scale 1 as a control
            Assert.AreEqual(0f, object0.RotY);
            Assert.AreEqual(1f, object0.ScaleX);
```
- [ ] **Step 2: コンパイルして失敗確認** — Run: `uloop compile --project-path ./moorestech_client` → Expected: `RotX` 未定義エラー
- [ ] **Step 3: `MapObjectLayoutMessagePack` を拡張**

```csharp
        [Key(5)] public float RotX { get; set; }
        [Key(6)] public float RotY { get; set; }
        [Key(7)] public float RotZ { get; set; }
        [Key(8)] public float ScaleX { get; set; }
        [Key(9)] public float ScaleY { get; set; }
        [Key(10)] public float ScaleZ { get; set; }

        public MapObjectLayoutMessagePack(int instanceId, string mapObjectGuid, float x, float y, float z,
            float rotX, float rotY, float rotZ, float scaleX, float scaleY, float scaleZ)
        {
            InstanceId = instanceId;
            MapObjectGuid = mapObjectGuid;
            X = x;
            Y = y;
            Z = z;
            RotX = rotX;
            RotY = rotY;
            RotZ = rotZ;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
        }
```

（旧5引数コンストラクタは削除し、呼び出し側を全て新形へ更新する。デフォルト引数は使わない）

- [ ] **Step 4: `GetMapDataProtocol.cs` の送信側を更新**

```csharp
                foreach (var mapObject in _mapInfoJson.MapObjects)
                    mapObjects.Add(new MapObjectLayoutMessagePack(
                        mapObject.InstanceId, mapObject.MapObjectGuidStr,
                        mapObject.X, mapObject.Y, mapObject.Z,
                        mapObject.RotX, mapObject.RotY, mapObject.RotZ,
                        mapObject.ScaleX, mapObject.ScaleY, mapObject.ScaleZ));
```

- [ ] **Step 5: コンパイル** — Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0（旧コンストラクタ利用箇所が他にあればここで露出するので全て更新）
- [ ] **Step 6: テスト実行** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GetMapDataProtocolTest"` → Expected: PASS
- [ ] **Step 7: コミット** — `git commit -m "feat(protocol): va:mapData LayoutへTransform3要素を追加"`

---

### Task 4: クライアントでTransformを適用する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs:81`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/MapAuthoring/MapAuthoringExporter.cs:99-107`
- Modify: `moorestech_client/Assets/Scripts/Editor/MapAuthoring/MapAuthoringImporter.cs:79`

**Interfaces:**
- Consumes: `MapObjectLayoutMessagePack.RotX..ScaleZ`（Task 3）
- Produces: なし（表示適用の終端）

- [ ] **Step 1: Instantiateへ回転・スケールを適用** — `MapObjectGameObjectDatastore.cs` L81を置換:

```csharp
                    // サーバー生成のTransform3要素をそのまま適用する（ADR-0010）
                    // Apply the server-generated transform triple as-is (ADR-0010)
                    var rotation = Quaternion.Euler(layout.RotX, layout.RotY, layout.RotZ);
                    var instance = Instantiate(prefab, new Vector3(layout.X, layout.Y, layout.Z), rotation, transform);
                    instance.transform.localScale = new Vector3(layout.ScaleX, layout.ScaleY, layout.ScaleZ);
```

- [ ] **Step 2: HPバーの逆スケール補正** — `MapObjectGameObject.cs` の `Initialize` 末尾（rayTargets初期化の後）に追加:

```csharp
            // 個体スケールがUI表示に波及しないようHPバーは逆スケールで等倍を保つ
            // Counter-scale the HP bar so per-instance scaling never distorts the UI
            if (hpBarView)
            {
                var lossy = hpBarView.transform.parent.lossyScale;
                hpBarView.transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
            }
```

- [ ] **Step 3: MapAuthoringImporterでTransformを適用する** — `moorestech_client/Assets/Scripts/Editor/MapAuthoring/MapAuthoringImporter.cs:79` の position 設定に続けて rotation/scale も適用（往復でTransformが落ちないように）:

```csharp
                instance.transform.position = info.Position;
                instance.transform.rotation = info.Rotation;
                instance.transform.localScale = info.Scale;
```

- [ ] **Step 4: MapAuthoringExporterで実Transformを書き出す** — `BuildMapObjects` 内の `result.Add(...)` を置換:

```csharp
                var mapObjectTransform = mapObject.transform;
                var euler = mapObjectTransform.rotation.eulerAngles;
                var scale = mapObjectTransform.localScale;
                result.Add(new MapObjectInfoJson
                {
                    InstanceId = instanceId,
                    MapObjectGuidStr = guidString,
                    X = mapObjectTransform.position.x,
                    Y = mapObjectTransform.position.y,
                    Z = mapObjectTransform.position.z,
                    RotX = euler.x,
                    RotY = euler.y,
                    RotZ = euler.z,
                    ScaleX = scale.x,
                    ScaleY = scale.y,
                    ScaleZ = scale.z,
                });
```

（既存の `var position = mapObject.transform.position;` 行は削除）

- [ ] **Step 5: コンパイル** — Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
- [ ] **Step 6: templateワールド起動確認** — uloopでPlayMode起動（`uloop-control-play-mode`）し、`uloop get-logs --project-path ./moorestech_client --log-type Error` でエラー0・mapObjectが従来どおり表示されることをスクショで確認（R4）。PlayMode停止
- [ ] **Step 7: コミット** — `git commit -m "feat(client): mapObjectへ回転・スケールを適用しHPバーを逆スケール補正"`

---

### Task 5: MapMakingプリセットから樹種インベントリを抽出する

**Files:**
- Create: `scripts/mapmaking-parity/extract_mapmaking_species.py`
- Create: `scripts/mapmaking-parity/species-inventory.json`（実行結果・コミットする）

**Interfaces:**
- Consumes: `TmpUnityPjt/MapMaking/Assets/MapGenerator/Presets/Biomes/*.asset`（読み取り専用）、`moorestech_client/Assets/PersonalAssets/moorestech-client-private/**/*.meta`（guid逆引き）
- Produces: `species-inventory.json`:

```json
{
  "species": [
    {
      "key": "Redwood/Sequoia1",
      "prefabGuid": "<unity guid>",
      "prefabPath": "Assets/PersonalAssets/moorestech-client-private/BK/PureNature_Redwood/Prefabs/Trees/Sequoia1.prefab",
      "kind": "tree",
      "address": "Vanilla/Environment/Tree/Redwood/Sequoia1",
      "wrapperPath": "Assets/AddressableResources/Environment/Tree/Redwood/Sequoia1.prefab",
      "mapObjectGuid": "<uuid5決定論guid>",
      "mapObjectName": "Sequoia1"
    }
  ],
  "biomes": {
    "forest": { "prototypes": [ { "mapObjects": [{"mapObjectGuid": "..."}], "scaleHeightRange": [0.4, 0.8], "…schema準拠の全キー…": "…" } ] },
    "jungle": { "speciesFill": ["<guid>", "…"] },
    "woods": { "speciesFill": ["<guid>", "…"] }
  }
}
```

- [ ] **Step 1: スクリプトを書く** — 要点仕様（コード全文はこの仕様に従い実装する）:
  - Unity YAMLパース: `.asset` を `--- !u!` でドキュメント分割し、`yaml.SafeLoader` に `yaml.add_multi_constructor('tag:unity3d.com,2011:', ...)` と `!u!` 無視を登録して読む（pyyaml使用。`pip install pyyaml` 前提）
  - 対象: `Forest.asset` `Grassland.asset` `Savanna.asset` `Mesa.asset`（現行スキーマ: `treePlacement.prototypes[].prefabs[]` guid配列）+ `Jungle.asset` `Woods.asset`（旧スキーマ: `- prefab:` 単数。樹種リストのみ抽出）
  - `disabled: 1` のプロトタイプはtreePlacement出力から除外する（裁定: 有効4+旧プリセット意図採用）。ただし species 一覧には含める（R5: 登録は全樹種）
  - guid→パス: `moorestech_client/Assets/PersonalAssets/moorestech-client-private` 以下の全 `.prefab.meta` を先に走査して `guid→prefabパス` 辞書を作る。**未解決guidは即例外**（fail-fast）
  - kind判定: プレハブパスに `/Rocks/` を含む→`rock`、プレハブ名が `Pebble` で始まる→`pebble`、それ以外→`tree`
  - address/wrapperPath: `Vanilla/Environment/{Tree|Rock}/<パック短縮名>/<プレハブ名>`（パック短縮名は `PureNature_Redwood`→`Redwood`、`PureNature`→`Base`）。pebbleはRock側に置く
  - mapObjectGuid: `uuid.uuid5(uuid.UUID('a3c7e0d4-0000-4000-8000-mapmaking000'), f"moorestech.mapobject.{key}")` 形式の決定論採番（再実行で不変）。※名前空間UUIDはスクリプト内定数として有効なUUIDを1つ固定する
  - prototypesの変換: Unity YAMLのフィールド名→スキーマキーは同名（`VanillaSchema/mapGenerate/treePlacementConfig.yml` を正としてキー一覧をスクリプトに定数化）。`Vector2 {x,y}`→`[x,y]`。`prefabs` guid配列→順序保存で `mapObjects` guid配列。ネスト（`densityConfig`/`understoryConfig`/`rockProximityConfig`/`slopeFilter`/`curvatureFilter`/`clusterNoise`/`clusterNoise2`）も全キー転写。**スキーマに無いキー・見つからないキーは即例外**（黙って落とさない）
- [ ] **Step 2: 実行** — Run: `python3 scripts/mapmaking-parity/extract_mapmaking_species.py` → Expected: `species-inventory.json` 生成。件数目安: species 約94（tree約67/rock約24/pebble約3）、forest prototypes 7・grassland 18・savanna 2・mesa 2、jungle speciesFill 7・woods 4
- [ ] **Step 3: 既存portとの突合で検証** — forest prototype 0 の抽出結果と、現行 `../moorestech_master/.../master/generation.json` の `algorithmParam.forest.treePlacement.prototypes[0]` を `mapObjects` 以外のキーで比較するワンライナーを実行し、差分をレビューする（既存portは同じプリセット由来なので原則一致。差分があればMapMaking側のその後の調整であり、プリセット現在値を正とする）。注: 現行generation.jsonのforestは既に7プロトタイプ（0-4=木系、5-6=小石系）あり、突合は同種プロトタイプ同士で行う
- [ ] **Step 4: コミット** — `git add scripts/mapmaking-parity/ && git commit -m "feat: MapMakingプリセットの樹種インベントリ抽出"`

---

### Task 6: master map.json へ樹種・岩mapObjectを一括追記する

**Files:**
- Create: `scripts/mapmaking-parity/gen_map_master.py`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json`

**Interfaces:**
- Consumes: `species-inventory.json`（Task 5）
- Produces: master `map.json` の `mapObjects` に約94件追加（既存3件は不変）

- [ ] **Step 1: 生成スクリプトを書く** — 冪等（同guidが既にあれば置換）。テンプレートは既存値の複製（裁定: `.decisions/2026-08-16-新規mapObjectの採掘設定は既存値の複製で統一.md`）:

```python
#!/usr/bin/env python3
"""species-inventory.jsonからmaster map.jsonへmapObjectsを一括追記する（冪等）。"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
INVENTORY = ROOT / "scripts/mapmaking-parity/species-inventory.json"
MASTER = ROOT.parent / "moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/map.json"

WOOD_ITEM = "aafce615-6c30-48c4-a29e-3c5b3266748f"   # 原木（既存「木」のドロップ）
STONE_ITEM = "582040ec-093b-4c8e-8fe3-f4ec030cf1ca"  # 石（既存「小石」のドロップ）
TREE_TOOLS = [
    {"toolItemGuid": "4c5fefbd-60a4-42ea-b70a-38a83b96e25e", "damage": 25, "attackSpeed": 1},
    {"toolItemGuid": "76174235-48fb-4944-bca7-ad268385d68c", "damage": 10, "attackSpeed": 2},
]

def build_entry(species: dict) -> dict:
    kind = species["kind"]
    if kind == "pebble":
        return {
            "mapObjectGuid": species["mapObjectGuid"], "mapObjectName": species["mapObjectName"],
            "addressablePath": species["address"], "hp": 1, "earnItemHps": [0],
            "soundEffectType": "stone",
            "earnItems": [{"itemGuid": STONE_ITEM, "minCount": 1, "maxCount": 1}],
            "miningType": "PickUp", "earnItemHpInterval": 1, "miningParam": {"miningTools": []},
        }
    is_tree = kind == "tree"
    return {
        "mapObjectGuid": species["mapObjectGuid"], "mapObjectName": species["mapObjectName"],
        "addressablePath": species["address"], "hp": 100, "earnItemHps": [0],
        "soundEffectType": "tree" if is_tree else "stone",
        "earnItems": [{"itemGuid": WOOD_ITEM if is_tree else STONE_ITEM, "minCount": 1, "maxCount": 4}],
        "miningType": "Mining", "miningParam": {"miningTools": TREE_TOOLS},
        "earnItemHpInterval": 10,
    }

def main() -> None:
    inventory = json.loads(INVENTORY.read_text(encoding="utf-8"))
    master = json.loads(MASTER.read_text(encoding="utf-8"))
    by_guid = {o["mapObjectGuid"]: i for i, o in enumerate(master["mapObjects"])}
    added = replaced = 0
    for species in inventory["species"]:
        entry = build_entry(species)
        if entry["mapObjectGuid"] in by_guid:
            master["mapObjects"][by_guid[entry["mapObjectGuid"]]] = entry
            replaced += 1
        else:
            master["mapObjects"].append(entry)
            added += 1
    MASTER.write_text(json.dumps(master, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"added={added} replaced={replaced} total={len(master['mapObjects'])}")

if __name__ == "__main__":
    main()
```

- [ ] **Step 2: 実行** — Run: `python3 scripts/mapmaking-parity/gen_map_master.py` → Expected: `added=約94 replaced=0 total=約97`
- [ ] **Step 3: ロード検証** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectMaster"` でマスタロード系テストがPASS（該当テストが無ければ任意のCombinedTest 1本でマスタロードが通ることを確認）
- [ ] **Step 4: コミット** — moorestech側: スクリプトを `git commit -m "feat: 樹種・岩mapObjectのmaster生成スクリプト"`。master側: `git -C ../moorestech_master commit -am "feat: BK樹種・岩mapObjectを一括追加"`

---

### Task 7: ラッパープレハブの一括生成とAddressable登録

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/MapObjectWrapperGeneratorMenu.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperAddressableRegistrar.cs`
- Create: `moorestech_client/Assets/AddressableResources/Environment/Tree/**` `Rock/**`（生成物 約94プレハブ）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeTest/MapObjectAddressableLoadTest.cs`

**Interfaces:**
- Consumes: `species-inventory.json`（Task 5）、`MapObjectGameObject`（SerializeField: `outlineObject: GameObject` / `hpBarView: MapObjectHpBarView`）、`MapObjectRayTarget`
- Produces: 各speciesの `wrapperPath` にプレハブ（アドレス=`address`、グループ=`Vanilla Asset Group`）

ラッパー構造はBush.prefabの先行パターン（BKプレハブをネストしルートへコンポーネント追加）+ Tree.prefabのRayTarget構造:
1. ルート = `PrefabUtility.InstantiatePrefab(BKプレハブ)` の結果（名前=species名）。`MapObjectGameObject` をAddComponent
2. `Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab` を子としてネスト（`PrefabUtility.InstantiatePrefab(hpBarPrefab, root.transform)`）。位置はレンダラーboundsの頂部 `(0, bounds.max.y + 0.5f, 0)`
3. `Outline` 子GameObject: BKプレハブのLOD0配下の各MeshRenderer/MeshFilterを複製した子を持ち、マテリアルを全スロット `Assets/Asset/Common/Shader/Outline/Outline.mat` に差し替え。初期状態 `SetActive(false)`
4. `RayTargetCollider` 子GameObject: `BoxCollider(isTrigger=true)` を全レンダラー合成boundsで設定 + `MapObjectRayTarget` をAddComponent
5. `SerializedObject` で `outlineObject`→Outline、`hpBarView`→HPバーの `MapObjectHpBarView` を配線し `PrefabUtility.SaveAsPrefabAsset(root, wrapperPath)`
6. Addressable登録: `AddressableAssetSettingsDefaultObject.Settings` から `Vanilla Asset Group` を取得し `CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(wrapperPath), group)`、`entry.address = species.address`

- [ ] **Step 1: 失敗するテストを書く** — `MapObjectAddressableLoadTest.cs`（EditModeテスト。配置先ディレクトリの既存asmdefに従う）:

```csharp
using System.Collections.Generic;
using Client.Common.Asset;
using Core.Master;
using NUnit.Framework;
using UnityEngine;

public class MapObjectAddressableLoadTest
{
    // 全mapObjectのアドレスが実プレハブへ解決でき、authoring必須要素を持つことを検証する
    // Every mapObject address must resolve to a prefab carrying the required authoring pieces
    [Test]
    public void 全mapObjectのAddressablePathがロードできMapObjectGameObjectとRayTargetを持つ()
    {
        // MasterHolderロードは既存のマスタロードテストユーティリティに倣う（Tests内の前例を検索して同じ初期化を使う）
        var failures = new List<string>();
        foreach (var element in MasterHolder.MapObjectMaster.MapObjectElements)
        {
            var prefab = AddressableLoader.LoadDefault<GameObject>(element.AddressablePath);
            if (prefab == null) { failures.Add($"load失敗: {element.AddressablePath}"); continue; }
            if (prefab.GetComponent<Client.Game.InGame.Map.MapObject.MapObjectGameObject>() == null)
                failures.Add($"MapObjectGameObject無し: {element.AddressablePath}");
            // 既存Bushの欠落は既知バグで別タスクのため、新規生成分のみRayTargetを要求する
            // Existing Bush's missing RayTarget is a known separate-task bug; only generated wrappers require it
            if (element.AddressablePath.StartsWith("Vanilla/Environment/Tree/") || element.AddressablePath.StartsWith("Vanilla/Environment/Rock/"))
                if (prefab.GetComponentInChildren<Client.Game.InGame.Map.MapObject.MapObjectRayTarget>() == null)
                    failures.Add($"MapObjectRayTarget無し: {element.AddressablePath}");
        }
        Assert.IsEmpty(failures, string.Join("\n", failures));
    }
}
```

（`MapObjectMaster` の全要素列挙プロパティ名は実装時に `Core.Master.MapObjectMaster` を確認して合わせる。マスタロード初期化はTests内の既存前例に従う）

- [ ] **Step 2: テスト実行して失敗確認** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObjectAddressableLoadTest"` → Expected: FAIL（新アドレス約94件がload失敗）。**EditModeでのAddressables実ロード（`AddressableLoader.LoadDefault`）が動く前例は未確認**のため、動かない場合は `AddressableAssetSettings` のentry検査（アドレス→アセットguid→`AssetDatabase.LoadAssetAtPath`）へテスト実装を切り替える
- [ ] **Step 3: Editorスクリプト3ファイルを実装** — 上記構造どおり。`MapObjectWrapperGeneratorMenu.cs` は `[MenuItem("Tools/MapObjectWrapper/Generate All")]` で `species-inventory.json`（パスは `Application.dataPath + "/../../scripts/mapmaking-parity/species-inventory.json"`）を読み全件生成。各ファイル200行以下・エディタ専用のため `Editor/` 配下（asmdefはMapAuthoringと同じEditorアセンブリに同居）
- [ ] **Step 4: コンパイル** — Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
- [ ] **Step 5: 生成実行** — Run: `uloop execute-menu-item` で `Tools/MapObjectWrapper/Generate All`（またはexecute-dynamic-codeで同メソッド呼び出し）→ Expected: コンソールに生成件数ログ・エラー0（`uloop get-logs --log-type Error` で確認）
- [ ] **Step 6: テスト実行** — Run: Step 2と同じ → Expected: PASS
- [ ] **Step 7: コミット** — 生成プレハブ・`.meta`・AddressableAssetsData差分・Editorスクリプト・テストをまとめて `git commit -m "feat(client): BK樹種・岩のラッパープレハブ一括生成とAddressable登録"`

---

### Task 8: generation.json の treePlacement をプリセット同期する

**Files:**
- Create: `scripts/mapmaking-parity/gen_generation_treeplacement.py`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json`
- Modify: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: `species-inventory.json` の `biomes`（Task 5）
- Produces: forest/grassland/savanna/mesa の `treePlacement.prototypes` 全置換、jungle/woods の各prototypeの `mapObjects` を speciesFill で置換

- [ ] **Step 1: スクリプトを書く** — 仕様:
  - forest/grassland/savanna/mesa: `algorithmParam.<biome>.treePlacement.prototypes` を inventory の prototypes で**全置換**（disabled除外済み・スキーマ全キー持ち）
  - jungle/woods: 既存prototypesの配置パラメータは維持し、各prototypeの `mapObjects` 配列だけを `speciesFill` の全guid（等確率）で置換（裁定: 樹種リストのみ意図採用・バイオームは無効のまま）
  - 書き出し後、スキーマ非準拠キーが無いことを `treePlacementConfig.yml` のキー集合と突合して検証（不一致は例外）
- [ ] **Step 2: 実行** — Run: `python3 scripts/mapmaking-parity/gen_generation_treeplacement.py` → Expected: 置換サマリ表示（forest 7 / grassland 18 / savanna 2 / mesa 2 prototypes）
- [ ] **Step 3: サーバー生成テストで検証** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGeneration|TreeRuntimeConfig"` → Expected: PASS（ロード不能な参照guid・キー欠落があればここで落ちる）
- [ ] **Step 4: masterコミットとピン更新** — `git -C ../moorestech_master commit -am "feat: treePlacementをMapMakingプリセット同期"`。masterのHEADハッシュを `.moorestech-external-revisions.json` に反映し `git commit -m "chore: masterピン更新(樹種同期)"`
- [ ] **Step 5: generatedワールド目視確認** — uloopでgeneratedワールドを起動（`.decisions/2026-08-12-generatedワールドプレイはエディタ専用ボタンで提供する.md` のエディタボタン経由）し、スクショで樹種・サイズ・向きの多様性を確認、Errorログ0。停止してコミット（スクショは `../moorestech_logs` 側へ）

---

### Task 9: 草の距離場を構築しフィルタを有効化する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Detail/DetailDistanceFieldBuilder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Detail/DetailDistanceRadius.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Source/PlacementGuidTable.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/TerrainDetailBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Visual/Cache/TerrainVisualCacheFormat.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Terrain/DetailDistanceFieldBuilderTest.cs`

**Interfaces:**
- Consumes: `MapObjectLayoutMessagePack`（位置とguid）、`SdfMapGenerator.Generate(SpatialGrid, int, float, float, float)`（既存・`Game.MapGeneration.Pipeline.Generators.Util`）、`SpatialGrid`、`VanillaGeneratorAlgorithmParam`（treePlacement/objectConfigのguid集合）
- Produces:
  - `DetailDistanceRadius.ComputeMaxSearchRadius(BiomeDetailConfig config, bool forTree) : float`（MapMaking `SdfMapGenerator.ComputeMaxSearchRadius` の移植: 有効な distanceFilter の `range.y`+マージンの最大値。該当なしは0）
  - `DetailDistanceFieldBuilder.Build(IReadOnlyList<MapObjectLayoutMessagePack> layout, VanillaGeneratorAlgorithmParam param, TerrainGenerationConfig config, Vector2 tileOrigin) : (SpatialGrid treeGrid, SpatialGrid objectGrid)`（layoutをguidで分類: 全バイオームのtreePlacement参照guid→treeGrid、objectConfig参照guid→objectGrid。座標は `位置 - tileOrigin` のタイルローカル）

- [ ] **Step 1: 失敗するテストを書く** — `DetailDistanceFieldBuilderTest.cs`。アレンジは既存 `TerrainDetailBuilderTest.cs` のヘルパー（`CreateConfig`/`CreateVisualSections`/`CreateHeights`/`CreateBiomeIndices`）を複製して使う:

```csharp
[Test]
public void treePlacement参照guidの配置だけがtreeGridに入る()
{
    const string treeGuid = "00000000-0000-1111-0000-00000000000a";
    const string objectGuid = "00000000-0000-1111-0000-00000000000b";
    const string unrelatedGuid = "00000000-0000-1111-0000-00000000000c";

    // treePlacementにtreeGuid、objectConfigにobjectGuidだけを参照する最小paramを組む
    // Build a minimal param whose treePlacement references treeGuid and objectConfig references objectGuid
    var param = CreateParamWithGuids(treeGuid, objectGuid);
    var layout = new List<MapObjectLayoutMessagePack>
    {
        new(0, treeGuid, 12f, 0f, 22f, 0f, 0f, 0f, 1f, 1f, 1f),
        new(1, objectGuid, 5f, 0f, 5f, 0f, 0f, 0f, 1f, 1f, 1f),
        new(2, unrelatedGuid, 1f, 0f, 1f, 0f, 0f, 0f, 1f, 1f, 1f),
    };

    var (treeGrid, objectGrid) = DetailDistanceFieldBuilder.Build(layout, param, CreateConfig(), new Vector2(10f, 20f));

    Assert.That(treeGrid.Count, Is.EqualTo(1));
    Assert.That(objectGrid.Count, Is.EqualTo(1));
    // tileOrigin(10,20)差引後の(2,2)に木が居ること（半径1m以内の近傍検索で確認）
    // The tree must sit at (2,2) after subtracting tileOrigin (10,20), checked via a 1m neighbor query
    Assert.That(treeGrid.HasNeighborWithin(2f, 2f, 1f), Is.True);
}

[Test]
public void 距離場を渡すとtreeDistanceFilter有効エントリの密度マップが変わる()
{
    // treeDistanceFilterをenabledにしたvisualSections（CreateVisualSectionsの複製+filter有効化）を使う
    // Use visualSections cloned from CreateVisualSections with treeDistanceFilter enabled
    var visualSections = CreateVisualSectionsWithTreeDistanceFilter();
    var treeGrid = new SpatialGrid(100f, 100f, 4f);
    treeGrid.Add(2f, 2f);

    var withField = TerrainDetailBuilder.Build(
        CreateConfig(), BiomeTypes, visualSections, CreateHeights(), CreateBiomeIndices(), null, null, treeGrid, null);
    var withoutField = TerrainDetailBuilder.Build(
        CreateConfig(), BiomeTypes, visualSections, CreateHeights(), CreateBiomeIndices(), null, null, null, null);

    // 距離場の有無で密度マップが変わらなければフィルタは配線されていない（null時代への回帰）
    // If the maps match with and without the field, the filter is not wired (regression to the null era)
    Assert.That(AreEqual(withField[0], withoutField[0]), Is.False);
}
```

（`CreateParamWithGuids`/`CreateVisualSectionsWithTreeDistanceFilter`/`AreEqual` はこのテストファイル内のヘルパーとして実装。filterの有効化は `DetailRuntimeConfigFactory` が受けるマスタ型または runtime型 `DetailEntry.treeDistanceFilter` を実装時に確認して設定する）

- [ ] **Step 2: テスト失敗確認** — Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DetailDistanceFieldBuilderTest"` → Expected: コンパイルエラー（型未定義）
- [ ] **Step 3: `DetailDistanceRadius` と `DetailDistanceFieldBuilder` を実装** — MapMaking `TmpUnityPjt/MapMaking/Assets/MapGenerator/Pipeline/Generators/Util/SdfMapGenerator.cs` の `ComputeMaxSearchRadius` を読み、同じ式で移植する（読み取り専用参照）。Builderはguid集合を `HashSet<string>` に集めてlayoutを1パス分類。**マスタ生成型（`VanillaGeneratorAlgorithmParam`）の直読は `Visual/Source/` 層に置く**（treePlacement/objectConfigのguid集合抽出は `Visual/Source/PlacementGuidTable.cs` を新設して担わせる。マスタ生成型直読を `BiomeVisualSectionTable` と同じSource層へ集約する前例準拠。`DetailDistanceFieldBuilder.Build` はこのテーブルの出力（guid集合2つ）を受け取る形にしてよい — その場合Interfacesのシグネチャは `Build(layout, treeGuids, objectGuids, config, tileOrigin)` に読み替える）
- [ ] **Step 4: `TerrainDetailBuilder.Build` の引数へ距離場を追加** — シグネチャに `SpatialGrid treeGrid, SpatialGrid objectGrid` を追加し、バイオームごとに `DetailDistanceRadius` で半径を求め `SdfMapGenerator.Generate(grid, detailResolution, terrainWidth, terrainLength, maxR)` で `float[,]` を生成して `GenerateForBiome` の末尾2引数（現在 `null, null`）へ渡す。半径0のバイオームはnullのまま（MapMaking Stage 5と同じ分岐）。**detail解像度（heightmapResolution-1）で生成する**（`DetailDensitySampler` が `[z,x]` をdetail座標で引くため。MapMakingはAlphamapResolutionだがmoorestechのサンプラ実装に合わせる）
- [ ] **Step 5: 呼び出し経路の配線** — `TerrainRuntimeBuilder.BuildAsync` 経路で `MapLayout.MapObjects` を `GeneratedTerrainSource` まで受け渡し、`RebuildAndCacheVisual` 内で Builder→`TerrainDetailBuilder.Build` に渡す。tileOrigin は既存の `SceneOrigin`/タイル原点計算を使う（`GeneratedTerrainSource` 内の既存フィールドを確認して同じ値を使用）
- [ ] **Step 6: キャッシュバージョンを上げる** — `TerrainVisualCacheFormat.cs` の version 2→3（距離場が密度に影響するため旧キャッシュを無効化）
- [ ] **Step 7: コンパイル+テスト** — `uloop compile` エラー0 → Step 2のテスト+`uloop run-tests --filter-value "TerrainDetail|DetailRuntime|DetailConfig"` PASS
- [ ] **Step 8: コミット** — `git commit -m "feat(client): 草のtree/object距離場を構築しdistanceFilterを有効化"`

---

### Task 10: 死にフラグ generateDetail / generateTexture を削除する

**Files:**
- Modify: `VanillaSchema/generation.yml:340,343`（キー削除。edit-schemaスキル参照）
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Config/Terrain/TerrainGenerationConfig.cs:75-76`
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Runtime/GenerationRuntimeConfigFactory.cs:115-116`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/generation.json`（`generateDetail`/`generateTexture` キー削除）

- [ ] **Step 1: スキーマからキー削除** — edit-schemaスキルの手順に従い `generation.yml` から2キーを削除。SourceGenerator再生成はコンパイルで走る
- [ ] **Step 2: コード側のフィールド・代入を削除** — `TerrainGenerationConfig.generateDetail`/`generateTexture` フィールドと `GenerationRuntimeConfigFactory` の代入2行を削除（`generateObject` は残す）
- [ ] **Step 3: master JSONからキー削除** — `python3 -c` ワンライナーで `algorithmParam` から2キーをpopして書き戻し、master側コミット
- [ ] **Step 4: コンパイル+全体テスト** — `uloop compile` エラー0 → `uloop run-tests --filter-value "MapGeneration|Terrain"` PASS
- [ ] **Step 5: コミット+ピン更新** — moorestech側コミット、masterピンを最終ハッシュへ更新

---

### Task 11: unityプレイ録画テストと外部監査による視覚検収

**Files:**
- 生成物: 録画・スクショ（`../moorestech_logs` 側へ保存。コードrepoにはコミットしない）

- [ ] **Step 1: generatedワールドをunityプレイ録画テストで起動** — `unity-playmode-recorded-playtest` スキルのプレイテストDSLでgeneratedワールドを起動・周囲を見回すシナリオを実行し録画を取得（masterデータは更新済みピンのworktreeを使用。スキーマ不整合の無言死に注意）
- [ ] **Step 2: MapMaking側の参照スクショを取得** — MapMaking側は変更せず、`TmpUnityPjt/MapMaking` の既存出力スクショ（`Docs/`や過去成果物）を使う。無ければMapMakingをuloopで開きForest/Grassland付近のSceneスクショを撮る（読み取り専用操作のみ）
- [ ] **Step 3: 外部監査で突き合わせ** — codex-auditスキルで両スクショを渡し「樹種構成・密度・スケール分布・草の分布がMapMakingと同等か」を評価基準にして監査。指摘があれば該当タスクへ戻って修正（データ側の差異はスクリプト再実行で反映）
- [ ] **Step 4: 全テストスイート回帰** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject|MapData|MapGeneration|Terrain|Detail"` → Expected: PASS

---

### Task 12: moores-code-review（必須・省略不可）

- [ ] **Step 1:** 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘対応後、残課題はbdへ積む

---

## 判断記録（ADR）

設計セッションの裁定（正本）:
- [ADR-0010 mapObjectの配置データはTransform相当のフル3要素を運ぶ](../../adr/0010-mapobject-placement-carries-full-transform.md)
- [ADR-0011 MapMakingとの見た目同一化は樹種ごとの個別mapObject登録で行う](../../adr/0011-mapmaking-parity-via-per-species-mapobjects.md)
- `.decisions/2026-08-16-treePlacement同期は有効4バイオーム+旧プリセット意図採用.md`
- `.decisions/2026-08-16-MapMaking同一化に岩も含める.md`
- `.decisions/2026-08-16-新規mapObjectの採掘設定は既存値の複製で統一.md`
- `.decisions/2026-08-16-mapObject配置データはTransform相当の3要素を通す.md`
- `.decisions/2026-08-16-templateマップは形式移行のみで見た目現状維持.md`
- `.decisions/2026-08-16-草の距離場フィルタ復元を今回スコープに含める.md`
- `.decisions/2026-08-16-BushのRayTarget欠落修正は別タスクに切る.md`（本planのスコープ外根拠）

レンズ該当ファイルの改修判断（ledger-gate掲載義務分）:
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/MapObjectLayoutMessagePack.cs`: 既存Key(0)-(4)に連番でKey(5)-(10)を追加し旧コンストラクタは削除・全呼び出し側一括更新（出所: ユーザー裁定 2026-08-16 Transform3要素 + agent前提: プロトコル互換フォールバック禁止の既存原則）
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs`: Layout応答へ新フィールドを素通し転記するのみ。新規プロトコル・イベントは作らない（既存va:mapDataの拡張であり可変状態同期の3点セット新設対象ではない: 配置は起動時静的データ）（出所: agent前提・前例=既存Layout転記）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`: Instantiate時にTransform適用のみ追加。購読構造・初期化フローは不変（出所: agent前提・前例=既存InstantiateMapObjectsFromLayoutAsync）
- `VanillaSchema/generation.yml:340,343`: 死にフラグ`generateDetail`/`generateTexture`をスキーマから削除（どこからも読まれていない実測に基づく。`generateObject`は配線済みのため残す）（出所: agent前提・根拠=server/client全grepで参照0件、AGENTS.md「デバッグ/テスト専用publicを残さない」）

planning中の判断（出所: agent前提。異議があれば実装前に裁定へ）:
- 回転のワイヤ表現はオイラー角(度)のflat 6フィールド（`rotX..scaleZ`）: 既存 `x/y/z` のflat前例に合わせた。quaternionは非可読で、生成側はY回転のみのため精度問題なし
- 岩の採掘値: hp100・石ドロップ1-4・miningToolsは木と同一guid（複製統一裁定の具体化。バランスは後からJSON編集）
- Jungle/Woodsは既存prototypeの配置パラメータを維持し `mapObjects` 配列のみ樹種リストで置換（旧スキーマにはdensityConfig相当が無いため移植不能。バイオーム無効のため実挙動に影響なし）
- 距離場はワイヤに種別を追加せず、クライアントがgeneration masterのtreePlacement/objectConfig参照guidで分類する（ワイヤ拡張なしで正確に分類でき、Transform3要素裁定の範囲を守る）
- 距離場はdetail解像度で生成（MapMakingはAlphamapResolutionだが、moorestechのDetailDensitySamplerがdetail座標で直接引く実装のため。座標系の一致を優先）
- HPバーは逆スケール補正で等倍維持（MapMakingにHPバーは無く、スケール適用でUIが歪むのは同一化の意図外）
- TerrainVisualCacheのversionを2→3（距離場導入で密度マップが変わるため旧キャッシュは誤り）
- bendFactor破棄・sinkのY畳み込み: ADR-0010に記載
- unityプレイ録画テストはタスクごとには行わず最終検収（Task 11）に集約: 中間タスクの検証はユニットテスト+uloopスクショで足り、録画テストはポート占有と時間コストが大きい
- ラッパープレハブの配置は `AddressableResources/Environment/{Tree,Rock}/<パック>/`: Bush.prefab（AddressableResources直下）の前例に従いpublic側へ置く（BK実体への参照はguidのみでBush前例と同じ）
- マスタ生成型の直読は `Visual/Source/` 層に集約（`PlacementGuidTable` 新設。`BiomeVisualSectionTable` が唯一の直読点という既存制約を層単位の集約として維持）
- MapAuthoringImporterにもTransform適用を追加（export/import往復でrot/scaleが落ちるのは機能退化のため。既存identityデータでは無挙動変化）
- テストデータ（Tests.Module/TestMod配下2件＋Client.Tests/EditModeInPlayingTest/ServerData 1件）も移行対象に含める（テスト用DIコンテナが同じMapInfoJsonローダーを通るため。漏らすと欠損キーが黙って0になりScale=(0,0,0)の無言破壊）
- 新6フィールドは `Required.Always` でfail-fast化（出所: シミュレーター予測（判事レビュー2026-08-16）→agent採用。既存x/y/zに前例は無いが、移行漏れ=無言Scale0という実害クラスをロード時例外に変える。planの他所の即例外方針と整合）

## 機能パリティ死活表（現在使える操作が計画後も生きるか）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| templateワールドの起動・表示 | 生きる | 形式移行はidentity付与のみ。Task 4 Step 6で起動検証 |
| mapObjectの照準・採掘（既存3種） | 生きる | 既存プレハブ・guid・採掘フローは不変（Bushの既存不具合は現状維持・別タスク） |
| mapObjectの照準・採掘（新樹種・岩） | 新規に生きる | ラッパーにRayTarget/コライダーを含め、MapObjectAddressableLoadTestで担保 |
| generatedワールドの起動 | 生きる | Task 8 Step 5・Task 11で検証 |
| MapAuthoringのexport/import往復 | 生きる（強化） | exporter/importerともTransform対応（Task 4）。既存identityシーンは値が変わらない |
| 破壊状態の同期（va:mapObjectInfo） | 生きる | instanceId体系・プロトコル不変 |
| 既存セーブ（generated） | 開発フェーズにつき互換不要 | instanceId再採番の可能性あり。マイグレーション不要の運用ルールに従う |

## 配置と前例

| 決定 | 前例 |
|---|---|
| ラッパープレハブ構造（BKネスト+MapObjectGameObject+Outline+HPバー） | `moorestech_client/Assets/AddressableResources/Environment/Bush.prefab` |
| RayTarget構造（トリガーコライダー+MapObjectRayTarget） | `moorestech_client/Assets/AddressableResources/Environment/Tree.prefab` |
| プロトコル拡張（MessagePack Key追加・全呼び出し側一括更新） | `MapObjectLayoutMessagePack` 既存Key(0)-(4) / AGENTS.md「変更の波及を恐れない」 |
| 距離場生成 | `Game.MapGeneration/Pipeline/Generators/Util/SdfMapGenerator.cs`（既存port）+ MapMaking `TerrainGenerator.cs` Stage 5 |
| detail入力の供給 | `TerrainDetailBuilder`→`DetailRuntimeGenerator.GenerateForBiome` の既存null引数（供給口が既に設計されている） |
| masterデータのスクリプト生成 | `scripts/` 配下の既存運用（データ一括変換） |
| Editor一括生成メニュー | `moorestech_client/Assets/Scripts/Editor/MapAuthoring/`（MenuItem+SerializedObject配線の前例） |
