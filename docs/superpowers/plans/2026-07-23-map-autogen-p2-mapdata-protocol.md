# マップ自動生成 P2（va:mapData Layout＋mapObject実行時生成）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新プロトコル `va:mapData`（Layout）でマップレイアウト（spawn/mapObjects全量/mapVeins）をクライアントに送り、mapObjectをシーン事前ベイクから実行時Instantiate（map.ymlマスタのAddressablesアドレス解決）に切り替える。加えて鉱脈の露頭オブジェクト実行時生成と設置プレビュー中の範囲表示を実装する（親spec §5-4・ADR#1/#4）。

**Architecture:** サーバーは既存の `MapInfoJson`（DI登録済みシングルトン・レイアウトの真実源。P1で `mapVeins` 単一配列に刷新済み）をそのまま応答に詰めるだけの読み取り専用プロトコルを1本追加する（既存機構は無傷・受動的統合）。クライアントは `map.yml`（P1でmapObjects.ymlからリネーム済み）の mapObjects に必須 `addressablePath`・mapVeins に必須 `outcropAddressablePath` を追加し、`MapObjectGameObjectDatastore` をベイクリスト方式からLayout応答による実行時Instantiate方式へ置換、露頭も同型の新Datastoreで実行時生成する。状態同期（`va:mapObjectInfo`＋`MapObjectUpdateEventPacket`）は無改修で継続使用。

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
                                                                                  └→ MapVeinObjectDatastore【新設】 → 露頭Instantiate＋設置プレビュー範囲表示
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
| 5 | `map.yml` の mapObjects に `addressablePath`・mapVeins に `outcropAddressablePath`（**必須**・optional禁止） | `VanillaSchema/map.yml`（P1でリネーム済み） | `train.yml` L35 の addressablePath（ただしtrainはoptional。**本件は必須化＋全JSON更新**が規約準拠） | ok |
| 6 | ラッパーPrefab群（mapObject用＋vein露頭用） | `moorestech_client/Assets/PersonalAssets/moorestech-client-private/Addressable/Environment/`（露頭は `Environment/Vein/`） | ユーザー裁定済み（2026-07-23）: 有料アセットPrefabを1個ネストしたラッパー | ok |
| 7 | GUID→プレハブ解決＋キャッシュ | `MapObjectGameObjectDatastore` 内 | `TrainCarObjectFactory.cs`（master→AddressablePath→AddressableLoader.LoadDefault→Instantiate、Dictionary<Guid,GameObject>キャッシュ） | ok |
| 8 | ベイク2037 PrefabInstance撤去 | `Environment.prefab` | uloop execute-dynamic-code 経由のPrefab編集（規約） | ok |
| 9 | `MapVeinObjectDatastore`（露頭の実行時Instantiate・純ビジュアル） | `Client.Game/InGame/Map/MapVein/` | 本プランTask 5の `MapObjectGameObjectDatastore`（Layout走査→マスタ解決→Instantiate→フレーム分散）と同型。露頭は非インタラクティブのため instanceId 突合・状態同期なし（親spec ADR#5） | ok |
| 10 | 設置プレビュー中のvein範囲表示 | `Client.Game/InGame/Map/MapVein/`（表示サービス）＋PlaceSystemからの毎フレーム駆動 | `PlaceSystemStateController.ManualUpdate()` 駆動同族（moorestech原則: UI入力系は毎フレーム駆動サービス）。描画は既存 `MapVeinGameObjectService.DrawGizmo` のAABB計算を実行時レンダリングへ転用 | ok |

**検査4（機構選択）**: MapObjectGameObjectDatastore は「ベイクリスト＋instanceId突合」から「Layout受信＋Instantiate＋instanceId突合」へ。突合以降のロジック（`Initialize`/`OnUpdateMapObject`）は無傷で流用し、供給源だけ差し替える受動的統合。

### 機能パリティ（死活表）

| 現在使える操作 | P2後 | 根拠 |
|---|---|---|
| mapObject採取・破壊・HPバー | 生存 | instanceId突合以降の同期ロジック無改修。Layout応答のinstanceIdはmap.json由来で従来と同一値 |
| 落下復帰（SpawnPointObject） | 生存 | シーン内マーカーは撤去対象外 |
| 鉱脈の発見・設置位置決め | **新規に生存** | 露頭Instantiate＋設置プレビュー範囲表示（Task 6）。従来はエディタGizmoのみで実プレイでは不可視だった |
| **MapExportAndSetting（シーン→map.json再エクスポート）** | **退化** | ベイクmapObject撤去でシーン上の収集元が消える。**裁定事項**（下記） |
| 既存テスト群 | 生存 | サーバー側は読み取り専用プロトコル追加のみ |

### 新規パターン（ユーザーレビュー注目点）

1. **MapExportAndSetting の退役**: ベイク撤去後、テンプレートマップ（v8手作りmap.json）の再編集手段がシーンベイク経由では失われる。既存map.jsonデータ自体は残るため既存ワールドは無影響。提案: 本ツールは退役とし、以後のマップ編集は生成経路（generation.json）へ一本化。**実装開始前にユーザー裁定を得ること**（AskUserQuestionで「退役して生成経路一本化」「編集専用シーンを別途残す」の2択を提示）
2. **addressablePath / outcropAddressablePath の必須化**: train.yml はoptionalだが、本件はAGENTS規約（フォールバック禁止）に従い必須。v8 mod・TestModの全map.json（マスタ。旧mapObjects.json）を一括更新する

---

### Task 1: map.yml に addressablePath / outcropAddressablePath 追加＋全JSON更新

**Files:**
- Modify: `VanillaSchema/map.yml`（P1でmapObjects.ymlからリネーム済み）
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/<v8 mod>/master/map.json`（mapObjects・mapVeins全エントリ）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/.../map.json`（全エントリ）

**Interfaces:**
- Produces: `MapObjectMasterElement.AddressablePath`・`MapVeinMasterElement.OutcropAddressablePath`（string・必須。SourceGenerator再生成）

- [ ] **Step 1: edit-schema スキルを読み、map.yml の mapObjects 要素と mapVeins 要素にそれぞれ追加**

```yaml
    - key: addressablePath          # mapObjects要素
      type: string
    - key: outcropAddressablePath   # mapVeins要素（露頭プレハブ）
      type: string
```

（optional・default は付けない。欠損JSONはロード時に即失敗するのが正）

- [ ] **Step 2: SourceGenerator を起動し `MapObjectMasterElement.AddressablePath`・`MapVeinMasterElement.OutcropAddressablePath` 生成を確認**（edit-schemaスキルのトリガー手順）
- [ ] **Step 3: v8 mod と TestMod の map.json 全エントリに `"addressablePath"`（mapObjects）と `"outcropAddressablePath"`（mapVeins）を追加**

値は Task 2 で作るアドレス規約 `Vanilla/Environment/<mapObjectName>`・`Vanilla/Environment/Vein/<veinName>` を先行記入（例: `"addressablePath": "Vanilla/Environment/Bush01"`）。TestModはダミー値でよい（クライアント実ロードはしないため）。

- [ ] **Step 4: コンパイル＋既存マスタロード系テストの回帰確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject"`
Expected: 全PASS

- [ ] **Step 5: validate-schema スキルでバリデーション漏れ確認 → コミット**（moorestech・moorestech_master 両リポジトリ）

---

### Task 2: Addressable/Environment ラッパーPrefab群の作成（mapObject＋vein露頭）

**Files:**
- Create: `moorestech_client/Assets/PersonalAssets/moorestech-client-private/Addressable/Environment/<種類ごと>.prefab`（v8 mapObjects の全種類分）
- Create: `moorestech_client/Assets/PersonalAssets/moorestech-client-private/Addressable/Environment/Vein/<veinName>.prefab`（v8 mapVeins の全種類分。露頭＝純ビジュアルのためコンポーネント不要の見た目のみPrefab）
- Modify: Addressablesグループ（新規 `Environment Asset Group` を AddressableAssetsData に追加）

**Interfaces:**
- Produces: アドレス `Vanilla/Environment/<mapObjectName>` でロード可能なGameObject群（ルートに `MapObjectGameObject`。instanceId/guidは未設定=実行時に注入）＋ `Vanilla/Environment/Vein/<veinName>` の露頭Prefab群

- [ ] **Step 1: v8 map.json（マスタ）から種類一覧を抽出**（`jq '.mapObjects[].mapObjectName, .mapVeins[].veinName' map.json` で列挙し作業リスト化）
- [ ] **Step 2: uloop execute-dynamic-code で種類ごとにラッパーPrefabを作成**

処理内容: ①`Environment.prefab` 内の該当種の既存PrefabInstance 1個からソースプレハブ（有料アセット側）のGUIDを特定 ②新規GameObject（名前=mapObjectName）に `MapObjectGameObject` を付け、子にソースプレハブをネスト配置 ③`PrefabUtility.SaveAsPrefabAsset` で `.../Addressable/Environment/<name>.prefab` へ保存 ④AddressablesのEnvironment Asset Groupへ登録しアドレス `Vanilla/Environment/<name>` を設定。`MapObjectGameObject` の `outlineObject`/`hpBarView` 参照は既存ベイク個体の構成を踏襲して配線する。

- [ ] **Step 3: vein露頭Prefabを種類ごとに作成**（既存の鉱石岩・液体系の見た目アセットを流用した純ビジュアルPrefab。`Vanilla/Environment/Vein/<veinName>` でAddressables登録。テンプレートマップの手置きオブジェクトと併存しても違和感のないサイズ・見た目にする＝親spec ADR#8の吸収先）
- [ ] **Step 4: `AddressableLoader.LoadDefault<GameObject>(...)` が mapObject・露頭の全アドレスで成功することを execute-dynamic-code で全件検証**
- [ ] **Step 5: コミット**（moorestech本体は AddressableAssetsData の差分・private リポジトリは Prefab群をそれぞれコミット）

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
        [Key(4)] public List<VeinLayoutMessagePack> MapVeins { get; set; }
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
        [Key(0)] public string VeinGuid { get; set; }   // mapVeinsマスタのveinGuid（item/fluid区別はマスタから導出）
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

### Task 6: 鉱脈の露頭実行時生成＋設置プレビュー範囲表示

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinObjectDatastore.cs`（露頭の実行時Instantiate）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinRangeViewService.cs`（設置プレビュー中のAABB範囲表示）
- Modify: PlaceSystem側の駆動元（`PlaceSystemStateController` 系。`ManualUpdate()` から範囲表示サービスを駆動）

**Interfaces:**
- Consumes: `InitialHandshakeResponse.MapLayout.MapVeins`（Task 4）、`MasterHolder.MapVeinMaster`（P1）、`MapVeinMasterElement.OutcropAddressablePath`（Task 1）、アドレス `Vanilla/Environment/Vein/*`（Task 2）
- Produces: vein AABB中心XZの地表高さに露頭プレハブ（純ビジュアル・非インタラクティブ。親spec ADR#5）。設置プレビュー中のvein範囲の実行時表示

- [ ] **Step 1: MapVeinObjectDatastore を実装**（Task 5 の `MapObjectGameObjectDatastore` と同型: MapVeins走査→veinGuid→`MasterHolder.MapVeinMaster`→`OutcropAddressablePath`→`AddressableLoader.LoadDefault`→AABB中心XZの地表高さ（Terrain.SampleHeightまたはレイキャスト）へInstantiate・フレーム分散。instanceId突合・状態同期・破壊処理は持たない。**地表高さ取得に失敗した場合は無言でY=0等に落とさず即例外**＝フォールバック禁止規約。データ不正を起動時に顕在化させる）
- [ ] **Step 2: MapVeinRangeViewService を実装**（`PlaceSystemStateController.ManualUpdate()` からの毎フレーム駆動＝スポイト設計と同族。ただしPlaceSystem側は「プレビュー中か」を渡して駆動するだけで、veinの解決・対象絞り込み・描画詳細はサービス内部に封じ込める。設置プレビュー中のみ、カメラ周辺のvein AABBを半透明ボックスで実行時表示。AABB計算は既存 `MapVeinGameObjectService` のMin/Max計算を流用し、`Gizmos` 依存部はランタイムメッシュ/ラインレンダラーへ置換。表示のitem/fluid色分けは `MapVeinMaster.veinType` から導出）
- [ ] **Step 3: コンパイル → EditModeInPlayingTest で検証**（editmode-in-playing-test スキル準拠）: ①起動→露頭が全vein分生成され、代表1件の位置がAABB中心XZと一致 ②プレビュー開始→範囲表示オブジェクト生成 ③プレビュー終了→該当オブジェクトがシーンから0件（破棄漏れなし） ④開始→終了を3回連続→残存数が蓄積しない（再入で二重生成しない）
- [ ] **Step 4: コミット**

---

### Task 7: Environment.prefab からmapObjectベイクを撤去

**Files:**
- Modify: `moorestech_client/Assets/Asset/Common/Prefab/Environment/Environment.prefab`（uloop execute-dynamic-code 経由のみ）

- [ ] **Step 1: execute-dynamic-code で Environment.prefab 内の `MapObjectGameObject` を持つ子（PrefabInstance 2037個）を全削除して保存**（Terrain・TerrainData参照・SpawnPointObject は残す）
- [ ] **Step 2: プレイモード起動で重複生成が無いこと（総数2011のまま）と採取動作を確認**
- [ ] **Step 3: コミット**（.prefab と自動更新された .meta）

---

### Task 8: 統合プレイテストと最終レビュー

- [ ] **Step 1: unity-playmode-recorded-playtest スキルで「起動→mapObject採取→アイテム入手」「露頭を目印に掘削機設置→採掘」シナリオを録画付き実行**
- [ ] **Step 2: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**
- [ ] **Step 3: 指摘反映 → pr-create**
