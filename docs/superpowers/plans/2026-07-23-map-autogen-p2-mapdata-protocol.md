# マップ自動生成 P2（va:mapData Layout＋mapObject実行時生成）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新プロトコル `va:mapData`（Layout）でマップレイアウト（spawn/mapObjects全量/veins）をクライアントに送り、mapObjectをシーン事前ベイクから実行時Instantiate（mapObjectsマスタのAddressablesアドレス解決）に切り替える。

**Architecture:** サーバーは既存の `MapInfoJson`（DI登録済みシングルトン・レイアウトの真実源）をそのまま応答に詰めるだけの読み取り専用プロトコルを1本追加する（既存機構は無傷・受動的統合）。クライアントは `mapObjects.yml` に必須 `addressablePath` を追加し、`MapObjectGameObjectDatastore` をベイクリスト方式からLayout応答による実行時Instantiate方式へ置換する。状態同期（`va:mapObjectInfo`＋`MapObjectUpdateEventPacket`）は無改修で継続使用。

**Tech Stack:** Unity 6 / C# / MessagePack（プロトコル）/ UniTask / Addressables / NUnit

**親スペック:** `docs/plans/map-autogen-world-design.md` §4-§5
**前提:** P1（`docs/superpowers/plans/2026-07-23-map-autogen-p1-server-generation.md`）完了・masterマージ済み。作業ブランチ: `feat/map-autogen-p2`

## Global Constraints

- 1ファイル200行以下。超える場合はディレクトリ分割（partial は如何なる条件でも絶対禁止）
- 1ディレクトリ10ファイルまで。超えたらサブディレクトリ化
- try-catch 基本禁止（外部境界のみ可・境界根拠コメント必須）。デフォルト引数禁止。単純getter/setterプロパティ禁止（Setは `SetHoge` メソッド）
- コメントは日本語→英語の2行セット（各1行）を3〜10行ごと。自明なコメント禁止
- イベントは Action でなく UniRx
- .metaファイル手動作成禁止。Prefab/シーン編集は `uloop execute-dynamic-code` 経由のみ（手編集禁止）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client`
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロードエラー時は45秒待ちリトライ
- スキーマ変更時は edit-schema スキル必読・`optional: true`＋デフォルトフォールバック禁止（必須化＋全JSON一括更新が正規手順）
- 各タスク完了ごとにコミット。コミット前に `git log`/`git status` で巻き込み確認

---

## 配置と前例

### データフロー地図

```
[MapInfoJson(DI・真実源)] → GetMapDataProtocol【読み手・新設】 → Layout応答 → InitialHandshakeResponse → MapObjectGameObjectDatastore【書き換え】 → 実行時Instantiate
既存: [MapObjectDatastore] → va:mapObjectInfo / MapObjectUpdateEventPacket → （変更なし・破壊/HP同期を継続）
```

GetMapDataProtocol は MapInfoJson の**読み手が1人増えるだけ**。既存の状態同期経路に抑止・迂回なし。

### 配置決定インベントリと前例

| # | 項目 | 配置先 | 前例（役割同型） | 判定 |
|---|---|---|---|---|
| 1 | `GetMapDataProtocol`（va:mapData, Mode enum） | `Server.Protocol/PacketResponse/` | `GetMapObjectInfoProtocol.cs`（同ファイルにRequest/Response MessagePack同梱・Key(2)から採番・[Obsolete]空ctor） | ok |
| 2 | PacketResponseCreator 登録1行 | `PacketResponseCreator.cs` L24-70の辞書 | 既存全プロトコル同形 | ok |
| 3 | `VanillaApiWithResponse.GetMapData()` | `Client.Network/API/VanillaApiWithResponse.cs` | `GetMapObjectInfo()`（1プロトコル=1メソッド） | ok |
| 4 | InitialHandShake 並列束へ追加（7→8要素） | 同上 L54-61 | `UniTask.WhenAll` タプルに1要素追加・`InitialHandshakeResponse` ctor拡張 | ok |
| 5 | `mapObjects.yml` に `addressablePath`（**必須**・optional禁止） | `VanillaSchema/mapObjects.yml` | `train.yml` L35 の addressablePath（ただしtrainはoptional。**本件は必須化＋全JSON更新**が規約準拠） | ok |
| 6 | ラッパーPrefab群 | `moorestech_client/Assets/PersonalAssets/moorestech-client-private/Addressable/Environment/` | ユーザー裁定済み（2026-07-23）: 有料アセットPrefabを1個ネストしたラッパー | ok |
| 7 | GUID→プレハブ解決＋キャッシュ | `MapObjectGameObjectDatastore` 内 | `TrainCarObjectFactory.cs`（master→AddressablePath→AddressableLoader.LoadDefault→Instantiate、Dictionary<Guid,GameObject>キャッシュ） | ok |
| 8 | ベイク2037 PrefabInstance撤去 | `Environment.prefab` | uloop execute-dynamic-code 経由のPrefab編集（規約） | ok |

**検査4（機構選択）**: MapObjectGameObjectDatastore は「ベイクリスト＋instanceId突合」から「Layout受信＋Instantiate＋instanceId突合」へ。突合以降のロジック（`Initialize`/`OnUpdateMapObject`）は無傷で流用し、供給源だけ差し替える受動的統合。

### 機能パリティ（死活表）

| 現在使える操作 | P2後 | 根拠 |
|---|---|---|
| mapObject採取・破壊・HPバー | 生存 | instanceId突合以降の同期ロジック無改修。Layout応答のinstanceIdはmap.json由来で従来と同一値 |
| 落下復帰（SpawnPointObject） | 生存 | シーン内マーカーは撤去対象外 |
| **MapExportAndSetting（シーン→map.json再エクスポート）** | **退化** | ベイクmapObject撤去でシーン上の収集元が消える。**裁定事項**（下記） |
| 既存テスト群 | 生存 | サーバー側は読み取り専用プロトコル追加のみ |

### 新規パターン（ユーザーレビュー注目点）

1. **MapExportAndSetting の退役**: ベイク撤去後、テンプレートマップ（v8手作りmap.json）の再編集手段がシーンベイク経由では失われる。既存map.jsonデータ自体は残るため既存ワールドは無影響。提案: 本ツールは退役とし、以後のマップ編集は生成経路（generation.json）へ一本化。**実装開始前にユーザー裁定を得ること**（AskUserQuestionで「退役して生成経路一本化」「編集専用シーンを別途残す」の2択を提示）
2. **addressablePath の必須化**: train.yml はoptionalだが、本件はAGENTS規約（フォールバック禁止）に従い必須。v8 mod・TestModの全mapObjects.jsonを一括更新する

---

### Task 1: mapObjects.yml に addressablePath 追加＋全JSON更新

**Files:**
- Modify: `VanillaSchema/mapObjects.yml`
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/<v8 mod>/master/mapObjects.json`（全エントリ）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/.../mapObjects.json`（全エントリ）

**Interfaces:**
- Produces: `MapObjectMasterElement.AddressablePath`（string・必須。SourceGenerator再生成）

- [ ] **Step 1: edit-schema スキルを読み、mapObjects.yml の data 要素に追加**

```yaml
    - key: addressablePath
      type: string
```

（optional・default は付けない。欠損JSONはロード時に即失敗するのが正）

- [ ] **Step 2: SourceGenerator を起動し `MapObjectMasterElement.AddressablePath` 生成を確認**（edit-schemaスキルのトリガー手順）
- [ ] **Step 3: v8 mod と TestMod の mapObjects.json 全エントリに `"addressablePath"` を追加**

値は Task 2 で作るアドレス規約 `Vanilla/Environment/<mapObjectName>` を先行記入（例: `"addressablePath": "Vanilla/Environment/Bush01"`）。TestModはダミー値でよい（クライアント実ロードはしないため）。

- [ ] **Step 4: コンパイル＋既存マスタロード系テストの回帰確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject"`
Expected: 全PASS

- [ ] **Step 5: validate-schema スキルでバリデーション漏れ確認 → コミット**（moorestech・moorestech_master 両リポジトリ）

---

### Task 2: Addressable/Environment ラッパーPrefab群の作成

**Files:**
- Create: `moorestech_client/Assets/PersonalAssets/moorestech-client-private/Addressable/Environment/<種類ごと>.prefab`（v8 mapObjects の全種類分）
- Modify: Addressablesグループ（新規 `Environment Asset Group` を AddressableAssetsData に追加）

**Interfaces:**
- Produces: アドレス `Vanilla/Environment/<mapObjectName>` でロード可能なGameObject群。ルートに `MapObjectGameObject` コンポーネントを付与（instanceId/guidは未設定=実行時に注入）

- [ ] **Step 1: v8 mapObjects.json から種類一覧を抽出**（`jq '.data[].mapObjectName' mapObjects.json` で列挙し作業リスト化）
- [ ] **Step 2: uloop execute-dynamic-code で種類ごとにラッパーPrefabを作成**

処理内容: ①`Environment.prefab` 内の該当種の既存PrefabInstance 1個からソースプレハブ（有料アセット側）のGUIDを特定 ②新規GameObject（名前=mapObjectName）に `MapObjectGameObject` を付け、子にソースプレハブをネスト配置 ③`PrefabUtility.SaveAsPrefabAsset` で `.../Addressable/Environment/<name>.prefab` へ保存 ④AddressablesのEnvironment Asset Groupへ登録しアドレス `Vanilla/Environment/<name>` を設定。`MapObjectGameObject` の `outlineObject`/`hpBarView` 参照は既存ベイク個体の構成を踏襲して配線する。

- [ ] **Step 3: `AddressableLoader.LoadDefault<GameObject>("Vanilla/Environment/<name>")` が全種で成功することを execute-dynamic-code で全件検証**
- [ ] **Step 4: コミット**（moorestech本体は AddressableAssetsData の差分・private リポジトリは Prefab群をそれぞれコミット）

---

### Task 3: GetMapDataProtocol（サーバー・va:mapData Layout）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（登録1行）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`

**Interfaces:**
- Produces:

```csharp
public class GetMapDataProtocol : IPacketResponse
{
    public const string ProtocolTag = "va:mapData";

    // リクエスト。ModeはLayoutのみ（TerrainChunkはP3で追加）
    // Request; Mode has Layout only for now (TerrainChunk arrives in P3)
    public enum MapDataMode { Layout }

    [MessagePackObject]
    public class RequestMapDataMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public int Mode { get; set; }   // MapDataMode
    }

    [MessagePackObject]
    public class ResponseMapDataLayoutMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3MessagePack Spawn { get; set; }
        [Key(3)] public List<MapObjectLayoutMessagePack> MapObjects { get; set; }
        [Key(4)] public List<VeinLayoutMessagePack> ItemVeins { get; set; }
        [Key(5)] public List<VeinLayoutMessagePack> FluidVeins { get; set; }
    }

    [MessagePackObject]
    public class MapObjectLayoutMessagePack
    {
        [Key(0)] public int InstanceId { get; set; }
        [Key(1)] public string MapObjectGuid { get; set; }
        [Key(2)] public float X { get; set; }
        [Key(3)] public float Y { get; set; }
        [Key(4)] public float Z { get; set; }
    }

    [MessagePackObject]
    public class VeinLayoutMessagePack
    {
        [Key(0)] public string Guid { get; set; }   // veinItemGuid or veinFluidGuid
        [Key(1)] public int MinX { get; set; } ... [Key(6)] public int MaxZ { get; set; }
    }
}
```

実装は creating-server-protocol スキルに従う。データ源は DIの `MapInfoJson`（`serviceProvider.GetService<MapInfoJson>()`）— レイアウトは静的データなので Datastore を経由しない（`MapObjectDatastore` 等と同じ「MapInfoJsonの読み手」前例）。Mode は switch で分岐し、未知値は例外。

- [ ] **Step 1: PacketTestを書く**（creating-server-tests スキル準拠。TestModで起動→Layoutリクエスト→mapObjects件数・先頭要素のguid/座標・veinのAABBが map.json と一致することをAssert）
- [ ] **Step 2: FAIL確認 → GetMapDataProtocol 実装＋PacketResponseCreator登録 → PASS確認**
- [ ] **Step 3: コンパイル・コミット**

---

### Task 4: クライアントAPI（GetMapData＋ハンドシェイク束へ追加）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs`（InitialHandshakeResponse）

**Interfaces:**
- Produces: `InitialHandshakeResponse.MapLayout`（`ResponseMapDataLayoutMessagePack` 型）

- [ ] **Step 1: `GetMapData(CancellationToken ct)` を追加**（`GetMapObjectInfo` L66-71 と同形。RequestにMode=Layoutを積む）
- [ ] **Step 2: `InitialHandShake()` の `UniTask.WhenAll` に8要素目として追加し、`InitialHandshakeResponse` のctorとフィールドを拡張**（既存7要素の展開形式を踏襲）
- [ ] **Step 3: コンパイル → コミット**

---

### Task 5: MapObjectGameObjectDatastore の実行時Instantiate化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs`

**Interfaces:**
- Consumes: `InitialHandshakeResponse.MapLayout`（Task 4）、`MapObjectMasterElement.AddressablePath`（Task 1）、アドレス `Vanilla/Environment/*`（Task 2）
- Produces: `MapObjectGameObject.SetRuntimeIdentity(int instanceId, string mapObjectGuid)`（ベイク専用だったSerializeField値の実行時注入口）

- [ ] **Step 1: MapObjectGameObject に SetRuntimeIdentity を追加**

```csharp
// 実行時Instantiate用にID/GUIDを注入する（ベイク時代のSerializeField直接参照を置換）
// Injects identity for runtime instantiation (replaces baked SerializeField values)
public void SetRuntimeIdentity(int instanceId, string mapObjectGuid)
{
    this.instanceId = instanceId;
    this.mapObjectGuid = mapObjectGuid;
}
```

- [ ] **Step 2: Datastore の Construct を書き換え**

`[SerializeField] List<MapObjectGameObject> mapObjects` を削除。`Construct(InitialHandshakeResponse)` で: ①`handshakeResponse.MapLayout.MapObjects` を走査 ②guidごとに `MasterHolder.MapObjectMaster` から `AddressablePath` を引き `AddressableLoader.LoadDefault<GameObject>` でプレハブ取得（`Dictionary<Guid, GameObject>` キャッシュ、`TrainCarObjectFactory.ResolvePrefab` と同形） ③自transform配下へ `Instantiate(prefab, new Vector3(x,y,z), Quaternion.identity)` し `SetRuntimeIdentity` ④100個ごとに `await UniTask.Yield()`（2011個の起動スパイク対策・フレーム分散） ⑤全個体を `_allMapObjects` に登録後、従来どおり `handshakeResponse.MapObjects`（va:mapObjectInfo）で `Initialize(mapObjectInfo)`。`OnUpdateMapObject` は無改修。

- [ ] **Step 3: コンパイル → EditModeInPlayingTest で「起動→mapObjectが2011個生成→1個採取」を検証**（editmode-in-playing-test スキル準拠でテスト追加）
- [ ] **Step 4: コミット**

---

### Task 6: Environment.prefab からmapObjectベイクを撤去

**Files:**
- Modify: `moorestech_client/Assets/Asset/Common/Prefab/Environment/Environment.prefab`（uloop execute-dynamic-code 経由のみ）

- [ ] **Step 1: execute-dynamic-code で Environment.prefab 内の `MapObjectGameObject` を持つ子（PrefabInstance 2037個）を全削除して保存**（Terrain・TerrainData参照・SpawnPointObject は残す）
- [ ] **Step 2: プレイモード起動で重複生成が無いこと（総数2011のまま）と採取動作を確認**
- [ ] **Step 3: コミット**（.prefab と自動更新された .meta）

---

### Task 7: 統合プレイテストと最終レビュー

- [ ] **Step 1: unity-playmode-recorded-playtest スキルで「起動→mapObject採取→アイテム入手」シナリオを録画付き実行**
- [ ] **Step 2: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**
- [ ] **Step 3: 指摘反映 → pr-create**
