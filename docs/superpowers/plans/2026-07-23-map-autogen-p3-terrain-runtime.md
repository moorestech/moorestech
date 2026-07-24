# マップ自動生成 P3（TerrainChunk転送＋地形実行時構築＋ベイク撤去）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** サーバーのterrain/バイナリ（height/biome）を `va:mapData` の TerrainChunk モードで分割転送し、クライアントが受信データ＋generationマスタの見た目セクションから `TerrainData` を実行時構築、`Environment.prefab` の地形ベイクを撤去して seed 違いで別地形が表示されるようにする。

**Architecture:** P2で新設した `GetMapDataProtocol` に `TerrainChunk` モードを追加し（1ドメイン1プロトコル・enum分岐）、`WorldDataDirectory` の terrain/ ファイルをGZip圧縮チャンクで送る。クライアントはロード画面中に全チャンクを取得してローカルキャッシュへ保存し、MapMakingの `TerrainApplier` 適用部を移植した実行時構築で `TerrainData` を生成する。テクスチャ・草花は `MasterHolder.GenerationMaster` の見た目セクション（P1で統合済み）＋Addressablesアドレスから決定論生成する。template モードは既存TerrainDataアセットをAddressables経由でロードし同じマウント経路に載せる（経路一本化）。

**Tech Stack:** Unity 6 / C# / MessagePack / System.IO.Compression(GZip) / UniTask / Addressables / Unity Terrain API / NUnit

**親スペック:** `docs/plans/map-autogen-world-design.md` §1(cache) §4(TerrainChunk) §5-2,5-3
**前提:** P1・P2完了・masterマージ済み。作業ブランチ: `feat/map-autogen-p3`

## Global Constraints

- 1ファイル200行以下（partial絶対禁止）・1ディレクトリ10ファイルまで
- try-catch 基本禁止（外部境界のみ・根拠コメント必須）。デフォルト引数禁止。単純getter/setter禁止
- コメントは日本語→英語2行セット（各1行）を3〜10行ごと
- イベントはUniRx。Prefab/シーン編集は uloop execute-dynamic-code 経由のみ
- .cs変更後は `uloop compile --project-path ./moorestech_client` 必須
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- 永続化はGUID・可読JSON。**terrain/ と cache/ のみ画像相当データとしてバイナリ例外（spec §1明記済み）。キャッシュは削除しても再構築される派生データに限る**
- 各タスク完了ごとにコミット。巻き込み確認必須

---

## 配置と前例

### データフロー地図

```
[WorldDataDirectory/terrain/*.r16|*.bin] → GetMapDataProtocol(TerrainChunk)【読み手拡張】 → GZipチャンク → TerrainDataFetcher【新設・書き手】 → [クライアントcache/worlds/<worldId>/terrain/] → TerrainRuntimeBuilder【新設・読み手】 → TerrainData → EnvironmentRoot
見た目: [MasterHolder.GenerationMaster(見た目セクション)] + [Addressables(TerrainLayer/テクスチャ)] → Splatmap/Detail決定論生成 → 同TerrainData
```

### 配置決定インベントリと前例

| # | 項目 | 配置先 | 前例（役割同型） | 判定 |
|---|---|---|---|---|
| 1 | `MapDataMode.TerrainChunk` 追加＋チャンク応答 | `Server.Protocol/PacketResponse/GetMapDataProtocol.cs` | 規約「1ドメイン1プロトコル・Mode enum分岐」・byte[]運搬は `EntityMessagePack.EntityData` 前例 | ok |
| 2 | Layout応答へterrainメタ追加（mapMode/worldId/解像度/タイル数/チャンク数） | 同上（Keyフィールド追加） | MessagePackのKey追加は後方追加のみ（既存Key番号不変） | ok |
| 3 | `WorldDataDirectory` のDI公開（プロトコルから参照） | `MoorestechServerDIContainerGenerator` にSingleton登録 | `MapInfoJson` のSingleton登録と同形 | ok |
| 4 | `TerrainDataFetcher`（チャンク取得＋キャッシュ書込） | `Client.Starter/Initialization/` | `ServerConnectionInitializer.RunAsync()`（初期化パイプラインの並列RunAsyncユニット） | ok |
| 5 | クライアントキャッシュパス | `GameSystemPaths` に `GetWorldCacheDirectory(string worldId)` 追加 | `SaveFileDirectory` の `DirectoryCreator` パターン（Game.Pathsはクライアントasmdefから参照可・調査済み） | ok |
| 6 | `TerrainRuntimeBuilder`（TerrainData構築） | `Client.Game/InGame/Environment/Terrain/`（新ディレクトリ） | MapMaking `TerrainApplier.Apply`/`InfiniteTerrainManager.GenerateChunk`（new TerrainData→SetHeights→terrainLayers→SetDetailLayer→SetNeighbors の完成手順） | ok |
| 7 | Splatmap/Detail決定論生成の移植 | 同上配下 `Visual/` | MapMaking `DetailPlacementGenerator.GenerateForBiome`（決定論・System.Random＋座標由来ノイズ） | ok |
| 8 | 見た目データ源 | `MasterHolder.GenerationMaster` 見た目セクション | P1裁定済み（クライアント側SO分離管理は禁止・addressablePathでアセット解決） | ok |
| 9 | TerrainLayer/テクスチャ/既存TerrainDataのAddressables登録 | Environment Asset Group（P2 Task 2で新設済み） | `Vanilla/Environment/*` アドレス規約 | ok |
| 10 | ベイクTerrain撤去＋EnvironmentRootマウント | `Environment.prefab` | P2 Task 6と同じ execute-dynamic-code 編集 | ok |

**検査4（機構選択）**: シーン初期化への統合は `InitializeScenePipeline` の既存 `UniTask.WhenAll(server, modAsset, scene)` に fetch タスクを**並列ユニットとして1個足すだけ**（受動的統合）。シーンアクティブ化ゲート（`allowSceneActivation`）の制御構造は無改修で、fetch 完了を WhenAll の合流条件に加える。

### 新規パターン（ユーザーレビュー注目点）

1. **大容量バイナリの分割転送は前例なし**（調査で確認済み: byte[]フィールド前例はあるがチャンク分割プロトコルは初）。チャンクサイズ256KB（GZip後）・チャンク総数はLayoutメタで通知・順次リクエスト方式（クライアント主導pull。サーバーpushは既存プロトコル機構に無いため採らない）
2. **クライアントローカルキャッシュの新設**（`GameSystemDirectory/cache/worlds/<worldId>/`）: worldIdはLayoutメタで受け取る（world.jsonのseed＋createdAtから生成した安定ID）。キャッシュヒット判定は**ローカルterrainファイルの連結SHA256とLayoutメタ `TerrainHash` の照合**で行い、一致時はチャンク取得を全スキップ。不一致・欠損は区別せず無条件に全再取得（完了マーカーファイルは持たない——ハッシュ照合が完全性・鮮度・破損検出を兼ねる）。キャッシュは削除可能

### 機能パリティ（死活表）

| 現在使える操作 | P3後 | 根拠 |
|---|---|---|
| 現行v8マップの地形表示（Gaia製ベイク） | 生存 | templateモードは既存TerrainDataアセットをAddressables化して実行時マウント（見た目バイト同一） |
| mapObject採取（P2の実行時生成） | 生存 | 地形はmapObjectのY座標に影響しない（座標はmap.json由来の絶対値） |
| 落下復帰（SpawnPointObject） | 生存 | シーンマーカー残置。ただしgeneratedモードでは`Layout.Spawn`を優先する変更をTask 6に含む |
| シーンロード時間 | 退化リスク | 地形構築が同期的に重い場合はロード画面が伸びる。Task 8で実測し、閾値超過（+10秒）ならalphamap構築のフレーム分散を追加 |

---

### Task 1: Layout応答へterrainメタ追加（サーバー）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（`WorldDataDirectory` をSingleton登録・`MapInfoJson` 登録の隣）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs`（拡張）

**Interfaces:**
- Produces: `ResponseMapDataLayoutMessagePack` へ追加

```csharp
[Key(6)] public string MapMode { get; set; }            // "generated" | "template"
[Key(7)] public string WorldId { get; set; }            // world.jsonのseed+createdAtのSHA256先頭16桁
[Key(8)] public int TerrainResolution { get; set; }     // 0 = terrainなし(template)
[Key(9)] public int TerrainTileCount { get; set; }
[Key(10)] public int TerrainChunkTotal { get; set; }    // 全タイル合計チャンク数
```

（`[Key(11)] TerrainHash` はチャンク論理ストリームの定義と同居させるため Task 2 で追加する）

- [ ] **Step 1: テスト拡張**（generatedモードのTestModワールドでLayoutを取り、メタ5項目が world.json/terrainファイル実体と整合することをAssert）→ FAIL確認
- [ ] **Step 2: 実装**（world.jsonは `WorldDataDirectory.WorldMetaFilePath` から読む。templateモードは TerrainResolution=0/ChunkTotal=0）→ PASS確認
- [ ] **Step 3: コンパイル・コミット**

---

### Task 2: TerrainChunk モード実装（サーバー）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs`（Mode enum＋分岐）
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainChunkReader.cs`（ファイル読み・GZip・スライスの実処理。Protocol本体の200行制約対策）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainChunkTest.cs`

**Interfaces:**
- Produces:

```csharp
public enum MapDataMode { Layout, TerrainChunk }

[MessagePackObject]
public class RequestMapDataMessagePack : ProtocolMessagePackBase
{
    [Key(2)] public int Mode { get; set; }
    [Key(3)] public int ChunkIndex { get; set; }   // TerrainChunk時のみ使用
}

[MessagePackObject]
public class ResponseMapDataTerrainChunkMessagePack : ProtocolMessagePackBase
{
    [Key(2)] public int ChunkIndex { get; set; }
    [Key(3)] public byte[] Payload { get; set; }   // GZip圧縮済み断片
}
```

- チャンク定義: 全タイルの `height_x_y.r16` → `biome_x_y.bin` をタイル順に連結した論理ストリームを、**非圧縮256KB単位**でスライスし各スライスをGZip圧縮（`TerrainChunkReader.Read(WorldDataDirectory dir, int chunkIndex)`）。ChunkTotal＝ceil(総バイト/256KB)。並び順はLayoutメタの解像度・タイル数から復元可能（クライアント側で逆算）
- **TerrainHash**: 同じ論理ストリーム（非圧縮連結バイト列）のSHA256を `TerrainChunkReader.ComputeStreamHash(WorldDataDirectory dir)` で計算し、Layout応答に `[Key(11)] public string TerrainHash` として後方追加（初回計算・メモリキャッシュ。world.jsonへの保存はしない——terrain/実ファイルが真実源であり、保存値は差し替え・再生成で乖離しうるため）。templateモードは空文字
- 範囲外ChunkIndex・templateモードでのTerrainChunk要求は例外（サイレント空応答にしない）

- [ ] **Step 1: TerrainChunkReaderTest を書く**（一時WorldDataDirectoryにP1の`TerrainFileWriter`で書き→全チャンク読み→解凍連結→元ファイルとバイト一致・`ComputeStreamHash` が連結バイト列のSHA256と一致）→ FAIL → 実装 → PASS
- [ ] **Step 2: PacketTest**（TerrainChunkリクエスト→レスポンス解凍→元terrainファイルと一致・Layout応答の `TerrainHash` が実ファイルの再計算値と一致）→ FAIL → Protocol分岐実装 → PASS
- [ ] **Step 3: コンパイル・コミット**

---

### Task 3: クライアントキャッシュパスとチャンク取得

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs`（`GetWorldCacheDirectory` 追加）
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/TerrainDataFetcher.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（`GetTerrainChunk(int chunkIndex, CancellationToken ct)` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（WhenAllへ統合）

**Interfaces:**
- Produces:
  - `GameSystemPaths.GetWorldCacheDirectory(string worldId)` → `<GameSystemDirectory>/cache/worlds/<worldId>/`（DirectoryCreatorパターン）
  - `TerrainDataFetcher.RunAsync(InitialHandshakeResponse handshake)` — Layoutメタを見て: template→即return / ローカル `cache/worlds/<worldId>/terrain/` の連結SHA256（サーバーと同じ論理ストリーム順で再計算）が `Layout.TerrainHash` と一致→即return / それ以外→全チャンクを順次取得・解凍・元ファイル名で復元書込→**書込後に再ハッシュしてTerrainHashと照合、不一致なら例外で明示失敗**（転送破損をサイレント続行しない）
- Consumes: `GetMapDataProtocol.ResponseMapDataTerrainChunkMessagePack`（Task 2）

- [ ] **Step 1: GetTerrainChunk をクライアントAPIへ追加**（1プロトコル=1メソッド規約。Mode/ChunkIndexを積むだけ）
- [ ] **Step 2: TerrainDataFetcher 実装**（ハンドシェイク完了後に開始する必要があるため、`InitializeScenePipeline` の WhenAll 構成を「serverInitializer完了 → fetcher開始」の継続に変更。sceneLoader/modAssetLoaderとは引き続き並列）
- [ ] **Step 3: EditModeInPlayingTest**（generated小規模ワールドで起動→キャッシュディレクトリにterrainファイル復元→2回目起動でフェッチ0回（ログAssert）→キャッシュファイル1個を破損させて3回目起動→全再取得が走ることをログAssert）
- [ ] **Step 4: コンパイル・コミット**

---

### Task 4: 見た目アセットのAddressables整備とgeneration.jsonアドレス充填

**Files:**
- Modify: Addressables Environment Asset Group（TerrainLayer群・detailテクスチャ群・既存TerrainData `2a1ae938302ca4d6894c5201638fbba5` を登録）
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/<v8 mod>/master/generation.json`（見た目セクションの `addressablePath` 空欄を実アドレスで充填）

**Interfaces:**
- Produces: アドレス規約 `Vanilla/Environment/TerrainLayer/<name>`・`Vanilla/Environment/TemplateTerrainData`

- [ ] **Step 1: execute-dynamic-code でMapMakingが参照する全TerrainLayer/detailテクスチャをEnvironment Asset Groupへ登録**（P1エクスポータの警告ログ一覧が対象リスト）
- [ ] **Step 2: generation.json の addressablePath 空欄を全て充填**（P1 Task 6で「空欄一覧を警告ログ列挙」済みのため機械的に対応付け。充填後、空欄0件になったことを `jq '[.. | .addressablePath? // empty | select(.=="")] | length'` で確認）
- [ ] **Step 3: MasterHolderロード＋全アドレスのLoadDefault成功をexecute-dynamic-codeで全件検証 → コミット**（moorestech・moorestech_master両方）

---

### Task 5: TerrainRuntimeBuilder（クライアント地形構築）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`（height/biome読込→TerrainData構築の主フロー）
- Create: 同 `Terrain/TerrainFileLoader.cs`（r16/binのパース。P1 `TerrainFileWriter` の逆変換）
- Create: 同 `Terrain/Visual/SplatmapRuntimeGenerator.cs`（MapMaking SplatmapGenerator移植・入力をGenerationMaster見た目セクションに置換）
- Create: 同 `Terrain/Visual/DetailRuntimeGenerator.cs`（MapMaking DetailPlacementGenerator移植・同上）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UnitTest/TerrainFileLoaderTest.cs`（r16→float[,]往復）

**Interfaces:**
- Consumes: `GameSystemPaths.GetWorldCacheDirectory`（Task 3）、`MasterHolder.GenerationMaster` 見た目セクション（P1）、Addressablesアドレス（Task 4）
- Produces: `TerrainRuntimeBuilder.BuildAsync(string worldId, ResponseMapDataLayoutMessagePack meta, Transform environmentRoot)` — TerrainData生成→`new GameObject("Terrain")`＋`Terrain`/`TerrainCollider` をenvironmentRoot配下に生成

構築手順は MapMaking `TerrainApplier.Apply`＋`InfiniteTerrainManager.GenerateChunk` の完成形をそのまま踏襲（調査済みの必須設定: heightmapResolution→size→SetHeights→alphamapResolution→terrainLayers(Addressablesロード)→SetAlphamaps→SetDetailResolution(detailRes,16)→SetDetailScatterMode(InstanceCountMode)→detailPrototypes→SetDetailLayer→materialTemplate(URP Terrain Lit)→タイル間SetNeighbors）。決定論性: シードは `Layout.WorldId` 由来の `System.Random`（サーバー生成と独立してよい—見た目は権威データでない・spec §1）。

templateモード分岐: `meta.MapMode == "template"` なら構築せず `AddressableLoader.LoadDefault<TerrainData>("Vanilla/Environment/TemplateTerrainData")` を `Terrain` に割り当てる（経路一本化・見た目無変化）。

- [ ] **Step 1: TerrainFileLoaderTest**（P1 TerrainFileWriterの出力を読み戻しfloat[,]/byte[,]一致）→ FAIL → 実装 → PASS
- [ ] **Step 2: Splatmap/DetailのRuntimeGenerator移植**（アルゴリズム無変更。入力型だけGenerationMaster生成型に置換。200行規約でフィルタ計算はサブクラス分割）
- [ ] **Step 3: TerrainRuntimeBuilder 実装＋`MainGameStarter.StartGame` 後の初期化フローに組み込み**（`FinalizeInitializationAsync` 内・`GameInitializedEvent` 発火前）
- [ ] **Step 4: コンパイル・コミット**

---

### Task 6: Environment.prefab の地形ベイク撤去とスポーン優先順位

**Files:**
- Modify: `moorestech_client/Assets/Asset/Common/Prefab/Environment/Environment.prefab`（execute-dynamic-code: Terrain GameObject群を削除し空の `EnvironmentRoot` を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Player/PlayerObjectController.cs`（落下復帰: `Layout.Spawn` があればそれを優先、無ければ従来のSpawnPointObject）

- [ ] **Step 1: execute-dynamic-code でベイクTerrain撤去＋EnvironmentRootマウント追加 → 保存**
- [ ] **Step 2: PlayerObjectController の復帰座標をLayout優先に変更**（`InitialHandshakeResponse` は `MainGameStarter` 経由でDI済みの前例に従い注入）
- [ ] **Step 3: template/generated 両モードでプレイモード起動確認 → コミット**

---

### Task 7: 見た目キャッシュ（alphamap/detail再構築結果）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainVisualCache.cs`

**Interfaces:**
- Produces: `TerrainVisualCache.TryLoad / Save`（`cache/worlds/<worldId>/visual/` に splatmap/detail配列を生バイナリ保存。ヒット時はSplatmap/Detail生成をスキップ）。キャッシュ無効判定は**generationマスタのJSON文字列SHA256＋terrainHash（Task 2）**をキーに含める（マスタ変更・地形変更のどちらでも自動無効化。splatmapはheight+biome+マスタの派生物なので導出元を両方キーに含める）

- [ ] **Step 1: 実装＋TerrainRuntimeBuilderへ組み込み**（構築時間を1回目/2回目でログ計測）
- [ ] **Step 2: コンパイル・EditModeInPlayingTestで2回目起動の生成スキップをログAssert → コミット**

---

### Task 8: 統合検証と最終レビュー

- [ ] **Step 1: unity-playmode-recorded-playtest で「seed A起動→地形確認→終了→seed B起動→別地形」を録画検証**（seed違いで別地形＝P3の受け入れ条件）
- [ ] **Step 2: ロード時間実測**（現行ベイク比+10秒超ならalphamap構築のフレーム分散を追加投入）
- [ ] **Step 3: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**
- [ ] **Step 4: 指摘反映 → pr-create**

---

## 判断記録（ADR）

### ADR-1: terrainキャッシュの有効判定はworldIdではなくコンテンツハッシュ照合（2026-07-23 ユーザー裁定）

- **経緯**: 当初設計は「worldIdディレクトリ＋`terrain.complete` マーカー存在」でキャッシュヒット判定。worldId（seed＋createdAt由来）が変わらないままサーバー側terrainが変わるケース（生成アルゴリズム更新での再生成等）と、マーカー書込後のファイル破損を検出できない欠陥をユーザーが指摘
- **決定**: Layoutメタに `TerrainHash`（チャンク転送と同一の論理ストリームのSHA256）を追加し、クライアントはローカル再計算値との照合でヒット判定。`terrain.complete` マーカーは廃止（ハッシュ照合が完全性・鮮度・破損検出を兼ねる無料の上位互換）。ダウンロード直後にも再ハッシュ検証し転送破損を明示失敗にする
- **付随決定**: ハッシュはworld.jsonへ保存せずterrain/実ファイルから起動時計算（保存値は差し替えで乖離しうる——導出可能テスト）。ストリーム順序定義はTask 2のチャンク定義と共有し二重定義しない。Task 7見た目キャッシュのキーにもterrainHashを追加（splatmapの導出元はマスタ＋地形の両方）
- **棄却案**: world.jsonへのハッシュ永続化（乖離リスク）／マーカーとハッシュの併用（二重機構の理由なし）／ファイル単位ハッシュ（現規模数MBでは分割の必要なし。将来肥大時の逃げ道として言及のみ）
