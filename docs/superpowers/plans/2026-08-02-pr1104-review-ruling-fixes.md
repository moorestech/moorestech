---
spec: docs/plans/map-autogen-world-design.md
---

# PR #1104 独立レビュー裁定反映 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** PR #1104 独立レビューのユーザー裁定4件（D2: Layout応答メタの入れ子化 / D3: 鉱脈範囲表示のShow(bool)化 / D6: スポーン探索結果のログ可視化 / Critical5: 地表探査の集約）と、前提宣言（拒否権つき）のD1（初期化待機機構のUniTask統一＋露頭生成の明示呼び出し化）をコードへ反映する。

**Architecture:** ワイヤDTOの入れ子化はサーバー側 `ResponseMapDataMessagePack` のコンストラクタシグネチャを変えずに内部で新DTOへ畳み、クライアント側は復元メソッド `ToTerrainTransferMeta()` 1本へ寄せる。範囲表示は「状態プッシュ（Show）」と「フレーム駆動（ManualUpdate）」を分離する。残り2件は既存クラスへの最小追加。

**Tech Stack:** Unity 6 / C# / MessagePack / UniTask / NUnit / uloop CLI

**作業ブランチ:** `feat/map-autogen-p3`（worktree: `~/moorestech-worktrees/tree1`）

**裁定の出所:** PR #1104 独立レビューダイジェスト（/tmp/pr-review-1104/index.html）へのユーザーコメント 2026-08-02。spec『判断記録（ADR）』#11〜#14 に掲載済み。
**D1の扱い:** シミュレーター予測・確信高によりA+B併用を前提宣言（拒否権つき）としてTask 5に含める。ユーザーが拒否した場合はTask 5をスキップし裁定に従う（spec ADR#15）。
**スコープ外:** 独立レビューの他のCritical（namespace改名・キーリテラル集約・AABBテスト・SpawnClearanceマスタ化・ローカル関数化・コメント短縮等）は本planの対象外（別バッチ）。

## Global Constraints

- 1ファイル200行以下（partial絶対禁止）・1ディレクトリ10ファイルまで
- try-catch 基本禁止（外部境界のみ・根拠コメント必須）。デフォルト引数禁止（既存メソッドの既存デフォルト引数は現状維持）。単純getter/setter禁止
- コメントは日本語→英語2行セット（各1行）を3〜10行ごと。日本語目安: 処理・変数20字/メソッド30字
- イベントはUniRx。Funcの使用禁止
- .cs変更後は `uloop compile --project-path ./moorestech_client` 必須（Error 0）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- MessagePackの既存Key番号（2〜4）は不変。Key5以降は本PR新設のため作り直し可（ADR#11）
- 各タスク完了ごとにコミット。`git status`で巻き込み確認必須

---

### Task 1: Layout応答の地形メタ入れ子化（ADR#11・D2「たたむ」）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs`（ResponseMapDataMessagePack・113-160行）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/TerrainDataFetcher.cs`（RunAsync・31-72行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs`（CreateAsync・56-96行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs`（BuildAsync・44-95行）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs:91-99`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainChunkTest.cs:44-85`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainCacheFetchTest.cs:52-87`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainVisualCacheReuseTest.cs:51-94`

**Interfaces:**
- Consumes: `TerrainTransferMeta`（`Game.MapGeneration.Transfer`・不変）/ `Vector2MessagePack`（`Server.Util.MessagePack`・`X`/`Y` プロパティと `Vector2` を取るコンストラクタあり）
- Produces: `TerrainTransferMetaMessagePack`（`MapMode`/`WorldId`/`TerrainResolution`/`TerrainTileCount`/`TerrainChunkTotal`/`TerrainHash`/`WorldSeed`/`NoiseOrigin`/`SceneOrigin` と `TerrainTransferMeta ToTerrainTransferMeta()`）・`ResponseMapDataMessagePack.TerrainMeta`（Key(5)）。**コンストラクタ `ResponseMapDataMessagePack(spawn, mapObjects, mapVeins, TerrainTransferMeta, string)` のシグネチャは不変**（`CharacterTestDebug.cs:50`・`MapVeinRangeViewMaterialReuseTest.cs:124`・`TerrainCacheFetchTest.cs:86` の呼び出しは無改修で通る）

- [ ] **Step 1: 新DTOファイルを作成する**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs`（新規・.metaはUnityが生成するので作らない）:

```csharp
using System;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.MapData
{
    /// <summary>
    ///     Layout応答の地形メタ。転送メタとハッシュをワイヤ1キーへ畳む
    ///     Terrain meta of the Layout response, folding the transfer meta and its hash into one wire key
    /// </summary>
    [MessagePackObject]
    public class TerrainTransferMetaMessagePack
    {
        [Key(0)] public string MapMode { get; set; }
        [Key(1)] public string WorldId { get; set; }
        [Key(2)] public int TerrainResolution { get; set; }
        [Key(3)] public int TerrainTileCount { get; set; }
        [Key(4)] public int TerrainChunkTotal { get; set; }

        // 論理ストリーム全体のSHA256。地形なしワールドは空文字
        // SHA256 of the whole logical stream; empty for terrain-less worlds
        [Key(5)] public string TerrainHash { get; set; }
        [Key(6)] public int WorldSeed { get; set; }

        // 生成時のノイズ窓原点とシーン原点。名前付きの対で運びX/Zの取り違えを封じる
        // Generation-time noise window origin and scene origin as named pairs, preventing X/Z mix-ups
        [Key(7)] public Vector2MessagePack NoiseOrigin { get; set; }
        [Key(8)] public Vector2MessagePack SceneOrigin { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public TerrainTransferMetaMessagePack() { }

        public TerrainTransferMetaMessagePack(TerrainTransferMeta terrainMeta, string terrainHash)
        {
            MapMode = terrainMeta.MapMode;
            WorldId = terrainMeta.WorldId;
            TerrainResolution = terrainMeta.TerrainResolution;
            TerrainTileCount = terrainMeta.TerrainTileCount;
            TerrainChunkTotal = terrainMeta.TerrainChunkTotal;
            TerrainHash = terrainHash;
            WorldSeed = terrainMeta.WorldSeed;
            NoiseOrigin = new Vector2MessagePack(terrainMeta.Origins.NoiseOrigin);
            SceneOrigin = new Vector2MessagePack(terrainMeta.Origins.SceneOrigin);
        }

        // ワイヤ値から転送メタを組み直す唯一の入口。モード解釈を各所へ散らさない
        // The single entry rebuilding the transfer meta from wire values, keeping mode interpretation in one place
        public TerrainTransferMeta ToTerrainTransferMeta()
        {
            if (MapMode == WorldProvisioner.TemplateMapMode) return TerrainTransferMeta.CreateTemplate(WorldId, WorldSeed);
            if (MapMode == WorldProvisioner.GeneratedMapMode)
                return TerrainTransferMeta.CreateGenerated(
                    WorldId, TerrainResolution, TerrainTileCount, TerrainChunkTotal, WorldSeed,
                    new TerrainOrigins(
                        noiseOrigin: new Vector2(NoiseOrigin.X, NoiseOrigin.Y),
                        sceneOrigin: new Vector2(SceneOrigin.X, SceneOrigin.Y)));
            throw new InvalidOperationException($"[TerrainTransferMetaMessagePack] Unknown map mode '{MapMode}'.");
        }
    }
}
```

注意: `Vector2MessagePack` のプロパティ名は `X`/`Y`（`moorestech_server/Assets/Scripts/Server.Util/MessagePack/Vector2MessagePack.cs` で確認済み）。`TerrainOrigins` のコンストラクタは `noiseOrigin:`/`sceneOrigin:` の名前付き引数（`TerrainDataFetcher.cs:40-42` の既存呼び出しと同形）。

- [ ] **Step 2: ResponseMapDataMessagePack を1キーへ畳む**

`GetMapDataProtocol.cs` の `ResponseMapDataMessagePack` クラス（113行付近〜）で、Key(5)〜Key(15) の11プロパティ宣言（`MapMode`〜`TerrainSceneOriginZ`、各コメント行含む）を**すべて削除**し、次の1プロパティへ置き換える:

```csharp
            // 地形メタとハッシュの束。地形なしワールドはTerrainResolution=0・TerrainHash=""で表明される
            // Terrain meta and hash bundle; a terrain-less world shows TerrainResolution=0 and an empty TerrainHash
            [Key(5)] public TerrainTransferMetaMessagePack TerrainMeta { get; set; }
```

コンストラクタ本文の `MapMode = terrainMeta.MapMode;` 〜 `TerrainSceneOriginZ = ...;` の11行を次の1行へ置き換える（**シグネチャは不変**）:

```csharp
                TerrainMeta = new TerrainTransferMetaMessagePack(terrainMeta, terrainHash);
```

ファイル先頭の `using Game.MapGeneration.Transfer;` / `using Server.Protocol.PacketResponse.MapData;` は既存のまま。

- [ ] **Step 3: コンパイルしてクライアント側の参照エラー一覧を得る**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TerrainDataFetcher.cs` / `GeneratedTerrainSource.cs` / `TerrainRuntimeBuilder.cs` / テスト3本で `'ResponseMapDataMessagePack' に 'MapMode' の定義がありません` 系エラー。**このエラー一覧がStep 4以降の改修箇所の全数チェックリストになる**（一覧に載った箇所以外を触らない）

- [ ] **Step 4: TerrainDataFetcher を復元メソッド消費へ書き換える**

`TerrainDataFetcher.cs` の `RunAsync` 冒頭（31-43行）を次へ置き換える:

```csharp
        public async UniTask<int> RunAsync(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout)
        {
            var wireMeta = mapLayout.TerrainMeta;

            // templateモードのワールドは地形バイナリを持たないので取得対象が無い
            // A template-mode world owns no terrain binary, so there is nothing to fetch
            if (wireMeta.MapMode == WorldProvisioner.TemplateMapMode) return 0;

            // 未知モードはToTerrainTransferMeta内で例外になる。ここで独自分岐を持たない
            // Unknown modes throw inside ToTerrainTransferMeta; no local branching here
            var terrainMeta = wireMeta.ToTerrainTransferMeta();
            var cacheWorldDirectory = WorldDataDirectory.FromWorldRoot(GameSystemPaths.GetWorldCacheDirectory(terrainMeta.WorldId));
```

以降の同メソッド内の残存参照を置換する: `mapLayout.WorldId` → `terrainMeta.WorldId`（51行・55行のログ2箇所）/ `mapLayout.TerrainHash` → `wireMeta.TerrainHash`（61行・63行・72行の3箇所）。旧11引数の `TerrainTransferMeta.CreateGenerated(...)` 呼び出し（38-42行）は削除済みになる。`using UnityEngine;` は `Vector2` を使わなくなっても `Debug.Log` で必要なので残す。

- [ ] **Step 5: GeneratedTerrainSource.CreateAsync のシグネチャをドメイン型へ変える**

`GeneratedTerrainSource.cs:56` を次へ変更（呼び出し元はTerrainRuntimeBuilderのみ・Step 6で追従）:

```csharp
        public static async UniTask<GeneratedTerrainSource> CreateAsync(TerrainTransferMeta terrainMeta, string terrainHash)
```

メソッド内の参照を置換する:
- `config.seed = mapLayout.WorldSeed;` → `config.seed = terrainMeta.WorldSeed;`
- `config.worldOffsetX = mapLayout.TerrainNoiseOriginX;` → `config.worldOffsetX = terrainMeta.Origins.NoiseOrigin.x;`
- `config.worldOffsetZ = mapLayout.TerrainNoiseOriginZ;` → `config.worldOffsetZ = terrainMeta.Origins.NoiseOrigin.y;`
- `var sceneOrigin = new Vector2(mapLayout.TerrainSceneOriginX, mapLayout.TerrainSceneOriginZ);` → `var sceneOrigin = terrainMeta.Origins.SceneOrigin;`
- 解像度照合の `mapLayout.TerrainResolution`（2箇所） → `terrainMeta.TerrainResolution`
- `GameSystemPaths.GetWorldCacheDirectory(mapLayout.WorldId)` → `GameSystemPaths.GetWorldCacheDirectory(terrainMeta.WorldId)`
- visual cacheキーの `mapLayout.TerrainHash` → `terrainHash`

`using Server.Protocol.PacketResponse;` が未使用になったら削除する（`GetMapDataProtocol` 参照が他に無い場合のみ）。

- [ ] **Step 6: TerrainRuntimeBuilder のモード分岐をwireMeta経由にする**

`TerrainRuntimeBuilder.cs` の `BuildAsync` 内分岐（53-60行）を次へ置き換える:

```csharp
            var wireMeta = mapLayout.TerrainMeta;
            if (wireMeta.MapMode == WorldProvisioner.TemplateMapMode)
                await BuildTemplateTerrainAsync(environmentRoot, terrainMaterial);
            else if (wireMeta.MapMode == WorldProvisioner.GeneratedMapMode)
                await BuildGeneratedTerrainAsync(wireMeta.ToTerrainTransferMeta(), wireMeta.TerrainHash, environmentRoot, terrainMaterial);
            else
                // 未知のモードをgenerated扱いすると、地形の無いワールドでキャッシュ読み出しが不可解に落ちる
                // Treating an unknown mode as generated would fail obscurely in the cache read of a terrain-less world
                throw new InvalidOperationException($"[TerrainRuntimeBuilder] Unknown map mode '{wireMeta.MapMode}'.");
```

`BuildGeneratedTerrainAsync` のシグネチャを `(GetMapDataProtocol.ResponseMapDataMessagePack mapLayout, ...)` から `(TerrainTransferMeta terrainMeta, string terrainHash, Transform environmentRoot, Material terrainMaterial)` へ変更し、内部の `GeneratedTerrainSource.CreateAsync(mapLayout)` → `GeneratedTerrainSource.CreateAsync(terrainMeta, terrainHash)`、タイル列挙 `TerrainTransferMeta.EnumerateTileCoordinates(mapLayout.TerrainTileCount)`（91行） → `EnumerateTileCoordinates(terrainMeta.TerrainTileCount)`、完了ログの `tiles={mapLayout.TerrainTileCount}` → `tiles={terrainMeta.TerrainTileCount}` に置換する。`using Game.MapGeneration.Transfer;` を追加する（未参照なら）。

- [ ] **Step 7: テスト5本をTerrainMeta経由の読み出しへ追従させる**

1. `GetMapDataTerrainMetaTest.cs`: `response.MapMode` / `response.TerrainResolution` / `response.TerrainTileCount` / `response.TerrainChunkTotal` / `response.WorldId` / `response.WorldSeed` / `response.TerrainHash` / `response.TerrainNoiseOriginX` 等の読み出し**全箇所**を `response.TerrainMeta.MapMode` / `response.TerrainMeta.NoiseOrigin.X` / `response.TerrainMeta.SceneOrigin.Y` の形へ置換する（`TerrainNoiseOriginX`→`NoiseOrigin.X`・`TerrainNoiseOriginZ`→`NoiseOrigin.Y`・`TerrainSceneOriginX`→`SceneOrigin.X`・`TerrainSceneOriginZ`→`SceneOrigin.Y` の対応）。ファイル内をgrepし置換漏れ0を確認する
2. `TerrainCacheFetchTest.cs`: 52-53行の `mapLayout.MapMode`/`mapLayout.TerrainChunkTotal` → `mapLayout.TerrainMeta.*`。55-59行の手組み `TerrainTransferMeta.CreateGenerated(...)` ブロックを `var terrainMeta = mapLayout.TerrainMeta.ToTerrainTransferMeta();` の1行へ置き換える。60-87行の `mapLayout.WorldId`→`terrainMeta.WorldId`・`mapLayout.TerrainTileCount`/`TerrainResolution`→`terrainMeta.*`・`mapLayout.TerrainHash`（67・82・87行）→`mapLayout.TerrainMeta.TerrainHash`・79行の `mapLayout.TerrainChunkTotal`→`terrainMeta.TerrainChunkTotal`。86-87行の改ざんlayout生成はコンストラクタシグネチャ不変のためそのまま（`new string('0', mapLayout.TerrainMeta.TerrainHash.Length)` のみ変更）
3. `TerrainVisualCacheReuseTest.cs`: 51-53行・64行・69行・83行・94行の `mapLayout.MapMode`/`mapLayout.TerrainTileCount`/`mapLayout.WorldId` → `mapLayout.TerrainMeta.*`
4. `GetMapDataProtocolTest.cs:91-99`: templateワールドの既定値assert群 `response.MapMode`/`response.WorldId`/`response.TerrainResolution`/`response.TerrainTileCount`/`response.TerrainChunkTotal`/`response.WorldSeed`（6参照） → `response.TerrainMeta.*` へ機械的置換
5. `GetMapDataTerrainChunkTest.cs:44-85`: `response.TerrainTileCount`/`response.TerrainChunkTotal`/`response.TerrainHash`（7参照） → `response.TerrainMeta.*` へ機械的置換

- [ ] **Step 8: コンパイルとテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Error 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GetMapDataProtocol|GetMapDataTerrainMeta|GetMapDataTerrainChunk|TerrainCacheFetch|TerrainVisualCacheReuse"`
Expected: 全件PASS（EditModeInPlayingTestはPlayMode遷移でドメインリロードを起こす。以降のuloopで「Unity is reloading」が出たら45秒待ってリトライ）

- [ ] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/MapData/TerrainTransferMetaMessagePack.cs* \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetMapDataProtocol.cs \
  moorestech_client/Assets/Scripts/Client.Starter/Initialization/TerrainDataFetcher.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/Build/GeneratedTerrainSource.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Environment/Terrain/TerrainRuntimeBuilder.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainMetaTest.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataProtocolTest.cs \
  moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetMapDataTerrainChunkTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainCacheFetchTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Terrain/TerrainVisualCacheReuseTest.cs
git commit -m "refactor: Layout応答の地形メタ11フィールドをTerrainTransferMetaMessagePackへ入れ子化する (ADR#11)"
```

---

### Task 2: 鉱脈範囲表示を Show(bool)＋ManualUpdate() へ分離（ADR#12・D3）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/IMapVeinRangeView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinRangeViewService.cs`（ManualUpdate・75-77行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`（OnEnter/GetNextUpdate 99行/OnExit 138行）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/FakeMapVeinRangeView.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Map/MapVeinRangeViewMaterialReuseTest.cs:66,79-80`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MapVeinOutcropAndRangeViewTest.cs:143-152`

**Interfaces:**
- Produces: `IMapVeinRangeView { void Show(bool isVisible); void ManualUpdate(); }`。`Show` は表示状態の変化時プッシュ（OnEnter/OnExit）、`ManualUpdate` はカメラ追従の距離カリング用フレーム駆動（`BuildUndoService.ManualUpdate()` と同形）

- [ ] **Step 1: interfaceを2メソッドへ分離する**

`IMapVeinRangeView.cs` 全体を次へ置き換える:

```csharp
namespace Client.Game.InGame.Map.MapVein
{
    /// <summary>
    ///     鉱脈範囲表示の窓口。設置側は表示状態の変化と毎フレームの駆動だけを渡す
    ///     Entry point of the vein range view; the placement side pushes visibility changes and per-frame ticks only
    /// </summary>
    public interface IMapVeinRangeView
    {
        // 表示状態の変化時にだけ呼ぶ（OnEnter/OnExit）
        // Called only when visibility changes (OnEnter/OnExit)
        void Show(bool isVisible);

        // カメラ追従の距離カリング用。表示中のフレーム駆動
        // Per-frame tick for camera-following distance culling while visible
        void ManualUpdate();
    }
}
```

- [ ] **Step 2: サービス側を状態保持＋2メソッドへ書き換える**

`MapVeinRangeViewService.cs`: フィールドに `private bool _isVisible;` を追加し、`public void ManualUpdate(bool isPlacementPreviewing)`（75行）を次の2メソッドへ置き換える（内部スイープの `isPlacementPreviewing` 参照は `_isVisible` へ変更。`#region Internal` のローカル関数群は不変）:

```csharp
        public void Show(bool isVisible)
        {
            _isVisible = isVisible;
            // 非表示への遷移を次フレームまで残さない。離脱時の残存ボックスを即座に畳む
            // Never carry a hide transition into the next frame; stray boxes fold immediately on exit
            ManualUpdate();
        }

        public void ManualUpdate()
        {
            var cameraPosition = _mainCamera.transform.position;

            foreach (var entry in _entries)
            {
                // 非表示中は距離を問わず全消し。範囲内だけボックスを持たせ、外れたものはプールへ返す
                // While hidden everything goes, regardless of distance; only in-range veins keep a box and the rest return to the pool
                var isVisible = _isVisible && IsWithinVisibleRadius(entry.Bounds, cameraPosition);
                if (isVisible) ShowEntry(entry);
                else HideEntry(entry);
            }

            // （既存の #region Internal ローカル関数群をそのままこのメソッド内に維持する）
        }
```

XMLサマリコメント（72-74行「設置プレビュー中かだけを受け取り…」）は `Show` 側へ「表示状態を受け取り、対象veinの絞り込みと描画はこのクラス内で完結させる」へ更新する。

- [ ] **Step 3: PlaceBlockState を変化時プッシュ＋フレーム駆動へ変える**

1. `OnEnter`（44行〜）: `if (context.TryGetContext<IPlacementTarget>(...)) ...SetTarget(target);` の直後に追加:

```csharp
            // 設置ステート滞在中は範囲表示を出す。対象の有無はステート自体が保証する（ADR#12）
            // The range view shows for the whole placement state; the state itself guarantees a target exists (ADR#12)
            _mapVeinRangeView.Show(true);
```

2. `GetNextUpdate`（99行）: `_mapVeinRangeView.ManualUpdate(_placeSystemStateController.CurrentTarget != null);` とその直前の2行コメントを次へ置き換える:

```csharp
            // カメラ追従の距離カリングだけを駆動する。表示のON/OFFはOnEnter/OnExitがプッシュ済み
            // Drive only the camera-following distance culling; visibility was already pushed by OnEnter/OnExit
            _mapVeinRangeView.ManualUpdate();
```

3. `OnExit`（138行）: `_mapVeinRangeView.ManualUpdate(false);` → `_mapVeinRangeView.Show(false);`（直前の2行コメントは不変）

- [ ] **Step 4: テストダブルと既存テストを追従させる**

1. `FakeMapVeinRangeView.cs`:

```csharp
using System.Collections.Generic;
using Client.Game.InGame.Map.MapVein;

namespace Client.Tests.UIState
{
    /// <summary>
    ///     設置状態から鉱脈範囲表示へ渡るプッシュだけを記録するテスト用の代替実装
    ///     Test double that records only the pushes the placement state sends to the vein range view
    /// </summary>
    public class FakeMapVeinRangeView : IMapVeinRangeView
    {
        public readonly List<bool> ShowPushes = new();
        public int ManualUpdateCount { get; private set; }

        public void Show(bool isVisible)
        {
            ShowPushes.Add(isVisible);
        }

        public void ManualUpdate()
        {
            ManualUpdateCount++;
        }
    }
}
```

（`PreviewingPushes` の参照元は無し — `UIStateCameraInteractionTest.cs:120`・`UIStateFocusRestorationTest.cs:100` はコンストラクタ注入のみ。grepで確認してから改名すること）

2. `MapVeinRangeViewMaterialReuseTest.cs`: `service.ManualUpdate(true)`（66行・80行） → `service.Show(true)`、`service.ManualUpdate(false)`（79行） → `service.Show(false)`（Showが即時スイープするため挙動等価）
3. `MapVeinOutcropAndRangeViewTest.cs` の `DriveRangeViewFrames`（143-152行）: ループ内の `rangeView.ManualUpdate(isPreviewing);`（152行）を、ループ**前**の `rangeView.Show(isPreviewing);` 1回＋ループ内の `rangeView.ManualUpdate();` へ分離する（実運用と同じ呼び分けを通す）

- [ ] **Step 5: コンパイルとテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Error 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinOutcropAndRangeView|MapVeinRangeViewMaterialReuse|UIStateCameraInteraction|UIStateFocusRestoration"`
Expected: 全件PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/IMapVeinRangeView.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinRangeViewService.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs \
  moorestech_client/Assets/Scripts/Client.Tests/UIState/FakeMapVeinRangeView.cs \
  moorestech_client/Assets/Scripts/Client.Tests/Map/MapVeinRangeViewMaterialReuseTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MapVeinOutcropAndRangeViewTest.cs
git commit -m "refactor: 鉱脈範囲表示をShow(bool)の変化時プッシュとManualUpdate()のフレーム駆動へ分離する (ADR#12)"
```

---

### Task 3: スポーン探索結果のログ可視化（ADR#13・D6「設定ゼロでも世界は作られるべき」）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs`（`ResolveSpawnOffset`・150-163行）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/SpawnOffsetSceneSpaceTest.cs:22-40`

**Interfaces:**
- Consumes: `SpawnSearchResult.Success` / `.Diagnostics`（既存・不変）
- Produces: 生成ログ `[SpawnSearch] 成功|フォールバック\n<Diagnostics>`（移植元 `TmpUnityPjt/MapMaking/Assets/MapGenerator/InfiniteTerrainManager.cs:115` と同形）。**挙動変更なし**（フォールバック時も従来どおり生成継続。throwを追加しない — ユーザー裁定「設定ゼロでも世界は作られるべき」）

- [ ] **Step 1: 失敗するテストを書く（ログ検証）**

`SpawnOffsetSceneSpaceTest.cs` の `SpawnAndPlacementsAreInsideTileWhenSpawnSearchSucceeds` 内、`var output = AssertOutputIsInsideTile(generation);` の**直前**に追加:

```csharp
            // 生成ログに探索の成否と診断が残ることを固定する（ADR#13: フォールバックを無言にしない）
            // Pin that generation logs the search outcome and diagnostics (ADR#13: fallbacks are never silent)
            LogAssert.Expect(LogType.Log, new Regex(@"\[SpawnSearch\] 成功"));
```

ファイル先頭に `using System.Text.RegularExpressions;` と `using UnityEngine.TestTools;` を追加する。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpawnOffsetSceneSpaceTest"`
Expected: FAIL（`Expected log did not appear: [SpawnSearch] 成功`）

- [ ] **Step 3: ResolveSpawnOffset へログを追加する**

`VanillaGenerator.cs` の `ResolveSpawnOffset` 内、`var result = SpawnRegionFinder.Find(config, biomeTypes);` の直後に追加:

```csharp
            // 成否と診断を必ず残す。候補ゼロや設定不備でも生成は止めない（ADR#13）
            // Always record the outcome and diagnostics; zero candidates or bad settings never abort generation (ADR#13)
            Debug.Log($"[SpawnSearch] {(result.Success ? "成功" : "フォールバック")}\n{result.Diagnostics}");
```

（`using UnityEngine;` は既存。throw・分岐は追加しない）

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpawnOffsetSceneSpaceTest|WorldProvisioner"`
Expected: 全件PASS（WorldProvisionerTestは生成を通しで回すため、新ログがテストを壊さないことも同時に確認する）

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/VanillaGenerator.cs \
  moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/SpawnOffsetSceneSpaceTest.cs
git commit -m "feat: スポーン探索の成否と診断を生成ログへ必ず残す (ADR#13)"
```

---

### Task 4: 地表高さ探査を SlopeBlockPlaceSystem へ集約（ADR#14・「集約」）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs`（`GetGroundPoint` 周辺・58-70行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinObjectDatastore.cs`（`TryResolveGroundHeight`・116-131行）＋関連定数・using
- Test: 既存 `MapVeinOutcropAndRangeViewTest`（露頭のY座標=地表高さ検証が回帰テストになる。新規テスト不要）

**Interfaces:**
- Produces: `SlopeBlockPlaceSystem.TryGetGroundPoint(Vector3 pos, out Vector3 groundPoint) -> bool`（ログなし・`GroundLayerMask` 使用）。既存 `GetGroundPoint(Vector3, Color)` はこの薄いラッパーになる（miss時のみLogError・シグネチャ不変）

- [ ] **Step 1: SlopeBlockPlaceSystem へ Try系単一エントリポイントを追加する**

`SlopeBlockPlaceSystem.cs` の `GetGroundPoint`（58行）を次の2メソッドへ置き換える:

```csharp
        // 地表探査の単一エントリポイント。露頭など大量プローブ用にログ無しで成否をboolで返す
        // Single entry point of ground probing; bulk probes such as outcrops get the outcome as a bool without logging
        public static bool TryGetGroundPoint(Vector3 pos, out Vector3 groundPoint)
        {
            var checkRay = new Ray(new Vector3(pos.x, 1000, pos.z), Vector3.down);
            if (Physics.Raycast(checkRay, out var checkHit, 1500, GroundLayerMask))
            {
                groundPoint = checkHit.point;
                return true;
            }
            groundPoint = default;
            return false;
        }

        public static Vector3? GetGroundPoint(Vector3 pos, Color debugRayColor = default)
        {
            Debug.DrawRay(new Vector3(pos.x, 1000, pos.z), Vector3.down * 1000, debugRayColor, 3);

            if (!TryGetGroundPoint(pos, out var groundPoint))
            {
                Debug.LogError("地面が見つかりませんでした pos:" + pos + " layer:" + GroundLayerMask);
                return null;
            }
            return groundPoint;
        }
```

（`GetGroundPoint` の既存デフォルト引数はシグネチャ現状維持。レイ原点y=1000・距離1500・`GroundLayerMask` は旧実装と同値）

- [ ] **Step 2: MapVeinObjectDatastore の再実装を委譲へ置き換える**

`MapVeinObjectDatastore.cs` のローカル関数 `TryResolveGroundHeight`（116-131行）の本文を次へ置き換える:

```csharp
            bool TryResolveGroundHeight(float x, float z, out float groundHeight)
            {
                // 地表判定は設置系と同じ単一エントリポイントへ委譲する（ADR#14: 集約）
                // Ground probing delegates to the placement systems' single entry point (ADR#14)
                if (SlopeBlockPlaceSystem.TryGetGroundPoint(new Vector3(x, 0f, z), out var groundPoint))
                {
                    groundHeight = groundPoint.y;
                    return true;
                }

                groundHeight = 0f;
                return false;
            }
```

あわせて同ファイルから不要になったものを削除する:
- 定数 `GroundProbeStartHeight` / `GroundProbeDistance`（ファイル冒頭。他に参照が無いことをgrepで確認）
- `GroundGameObject` / `LayerConst` を参照するusing（`TryResolveGroundHeight` 以外で未使用の場合のみ）
- `using Client.Game.InGame.BlockSystem;` を追加する

挙動差の根拠: 旧実装のRaycastAll＋GroundGameObject判定＋最高点選択に対し、新実装は `GroundLayerMask` 限定の単一Raycast。地形は `TerrainObjectFactory.Create` がGroundレイヤーへ配置しており、上から降ろす単一レイの最初のヒット＝最高点なので露頭用途では等価（マスクが狭まる分、手前の非地面コライダー誤ヒットの余地はむしろ消える）。探査窓は旧2000f→委譲先1500fに縮み探査下限がy=-1000→y=-500へ上がるが、地形高さの実域（0〜数十m）では影響しない。

- [ ] **Step 3: コンパイルと回帰テストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Error 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinOutcropAndRangeView|PlayerFallRecoveryPosition"`
Expected: 全件PASS（露頭の接地検証と、GetGroundPoint既存経路の落下復帰テストの両方が回る）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/SlopeBlockPlaceSystem.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinObjectDatastore.cs
git commit -m "refactor: 地表高さ探査をSlopeBlockPlaceSystem.TryGetGroundPointへ集約する (ADR#14)"
```

---

### Task 5: 初期化待機機構のUniTask統一＋露頭生成の明示呼び出し化（ADR#15・D1 A+B併用）

**前提宣言（拒否権つき）:** A+B併用はシミュレーター予測・確信高（同セッション裁定「たたむ」「集約」＋AGENTS.md「変更の波及を恐れない」の直接適用。違ったら指摘してください）。ユーザーが拒否した場合このタスクはスキップする。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Common/IInitialEventApplyWaitTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs`（該当: 32行のbool宣言・Construct・ループ末尾105行）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs`（該当: 31行のbool宣言・79行のtrue代入）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinObjectDatastore.cs`（該当: Construct 42行〜・WaitForInitializationAsync 137行〜）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（該当: 279行のDI登録）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`（該当: FinalizeAsync/WaitAllInitialEventApplyAsync 全体）
- Modify: `.claude/skills/unity-playmode-recorded-playtest/scenarios/misc/map-object-runtime-instantiate.cs`（`.agents` / `.codex` の同名コピー2本も同様。`IsInitialEventApplied` の読み手）
- Test: 既存 `MapVeinOutcropAndRangeViewTest` / `TerrainCacheFetchTest` / `TerrainVisualCacheReuseTest` / `PlaytestBootEnvironmentTest`（フルブート経路の回帰）

**Interfaces:**
- Produces: `IInitialEventApplyWaitTarget { UniTask WaitForInitialApplyAsync(); }`（bool `IsInitialEventApplied` は廃止・例外が待機境界へ届く）/ `MapVeinObjectDatastore.StartOutcropInstantiation()`（露頭生成の明示開始。DI生成時の副作用を廃止）
- Consumes: `UniTask.WhenAny(UniTask, UniTask) -> UniTask<int>`（勝者index）・`UniTask.Preserve()`（複数await可能化）

- [ ] **Step 1: interfaceをUniTask版へ置き換える**

`IInitialEventApplyWaitTarget.cs` 全体:

```csharp
using Cysharp.Threading.Tasks;

namespace Client.Game.Common
{
    /// <summary>
    ///     初期イベント（ディスパッチ開始時にreplayされるsnapshot等）の適用完了を初期化パイプラインが待つ対象
    ///     A target the init pipeline waits on until its initial events (snapshots replayed on dispatch start) are applied
    /// </summary>
    public interface IInitialEventApplyWaitTarget
    {
        // DI登録された全対象の完了を初期化パイプラインが待つ。失敗は例外として待機境界へ届く
        // The init pipeline awaits every registered target; failures reach the waiting boundary as exceptions
        UniTask WaitForInitialApplyAsync();
    }
}
```

- [ ] **Step 2: MapObjectGameObjectDatastore を保持タスク方式へ変える**

1. フィールド: `public bool IsInitialEventApplied { get; private set; }`（32行）とその宣言コメント2行を次へ置き換える:

```csharp
        // 生成ループの完了と例外を初期化パイプラインがawaitできる形で保持する
        // Retain the instantiation loop's completion and exceptions for the initialization pipeline to await
        private UniTask _initialApplyTask;

        public UniTask WaitForInitialApplyAsync()
        {
            return _initialApplyTask;
        }
```

2. `Construct` 内の `InstantiateMapObjectsFromLayoutAsync().Forget();` → `_initialApplyTask = InstantiateMapObjectsFromLayoutAsync().Preserve();`（直前コメントの「fire-and-forget」を「保持タスク」へ言い換える）
3. ループ末尾の `IsInitialEventApplied = true;`（105行）とその直前コメント2行を削除する（タスク完了自体がゲート解除になる）

- [ ] **Step 3: TrainFullSnapshotEventNetworkHandler を完了ソース方式へ変える**

1. `public bool IsInitialEventApplied { get; private set; }`（31行）を次へ置き換える:

```csharp
        // スナップショット適用完了の通知口。イベント駆動でタスクを所有しないため完了ソースで表現する
        // Completion source signalling snapshot application; event-driven code owns no task of its own
        private readonly UniTaskCompletionSource _initialApplyCompletion = new();

        public UniTask WaitForInitialApplyAsync()
        {
            return _initialApplyCompletion.Task;
        }
```

2. `IsInitialEventApplied = true;`（79行） → `_initialApplyCompletion.TrySetResult();`

- [ ] **Step 4: MapVeinObjectDatastore の生成開始を明示呼び出しへ分離する**

1. クラス宣言へinterfaceを追加: `public class MapVeinObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget`（`using Client.Game.Common;` 追加）
2. フィールド `private UniTask _initializationTask;` → `private InitialHandshakeResponse _handshakeResponse; private UniTask? _initializationTask;`
3. `Construct`（42行〜）を「保存のみ」へ変え、生成開始を新メソッドへ移す:

```csharp
        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // 生成はTerrain構築後にFinalizerが明示開始する。DI解決の副作用で地表Raycastを走らせない（ADR#15）
            // Instantiation starts explicitly from the finalizer after terrain build; DI resolution must not fire ground raycasts (ADR#15)
            _handshakeResponse = handshakeResponse;
        }

        public void StartOutcropInstantiation()
        {
            // 完了と例外を待機機構がawaitできる形で保持する
            // Retain completion and exceptions in an awaitable form for the wait mechanism
            _initializationTask = InstantiateOutcropsFromLayoutAsync().Preserve();

            #region Internal
            //（既存の InstantiateOutcropsFromLayoutAsync 〜 TryResolveGroundHeight のローカル関数群をConstructからこのメソッド内へ移動し、
            //  handshakeResponse 参照を _handshakeResponse へ置換する）
            #endregion
        }
```

4. `WaitForInitializationAsync()`（137-140行）を次へ置き換える:

```csharp
        public UniTask WaitForInitialApplyAsync()
        {
            // 開始前の待機要求は順序バグ。既定値タスク（完了扱い）で素通りさせず失敗させる
            // Waiting before the start is an ordering bug; never let the default (completed) task slip through
            if (_initializationTask == null)
                throw new InvalidOperationException("[MapVeinObjectDatastore] StartOutcropInstantiation前に待機が要求されました");
            return _initializationTask.Value;
        }
```

5. `WaitForInitializationAsync` の全参照をgrepし、テスト側の呼び出しがあれば `WaitForInitialApplyAsync` へ追従させる（`Object.FindFirstObjectByType<MapVeinObjectDatastore>` 経由の待機がEditModeInPlayingTestに存在する場合、Finalizer実行後なので開始済みガードには当たらない）

- [ ] **Step 5: DI登録へ待機interfaceを追加する**

`MainGameStarter.cs`（該当: 279行のDI登録）: `builder.RegisterComponent(mapVeinObjectDatastore);` → `builder.RegisterComponent(mapVeinObjectDatastore).AsSelf().As<IInitialEventApplyWaitTarget>();`（直上278行のMapObject登録と同形）

- [ ] **Step 6: Finalizerの順序を入れ替え、待機をWhenAll一本化する**

`MainGameInitializationFinalizer.cs` の `FinalizeAsync` と `WaitAllInitialEventApplyAsync` を次へ置き換える（`using System;` を追加。`using Client.Game.InGame.Map.MapVein;` は開始呼び出しに使うため残す）:

```csharp
        private async UniTask FinalizeAsync()
        {
            var starter = UnityEngine.Object.FindFirstObjectByType<MainGameStarter>();

            var resolver = starter.StartGame(_serverResult.HandshakeResponse);
            new ClientDIContext(new DIContainer(resolver));
            WebUiHost.Game.WebUiGameBinder.Bind();

            // イベント適用開始を地形構築より前へ戻し、未生成個体宛イベントが捨てられる窓を地形構築時間分広げない（ADR#15）
            // Start event application before terrain build so the drop window for not-yet-spawned targets never widens by build time (ADR#15)
            (_serverResult.VanillaApi.Event as VanillaApiEvent)?.InitializeDispatch();

            // 露頭生成の地表Raycastより前にTerrainを構築し、物理シーンへ反映する
            // Build Terrain before outcrop surface raycasts and synchronize it into the physics scene
            await TerrainRuntimeBuilder.BuildAsync(_serverResult.HandshakeResponse.MapLayout, starter.EnvironmentRoot.transform);

            // 露頭生成はTerrain完成後に明示開始する。完了待ちは下のWhenAllが一括で担う（ADR#15）
            // Outcrop instantiation starts explicitly after the terrain is ready; the WhenAll below waits for it with the rest (ADR#15)
            resolver.Resolve<MapVeinObjectDatastore>().StartOutcropInstantiation();

            await WaitAllInitialApplyAsync(resolver);
            starter.RestoreLoginState(_serverResult.HandshakeResponse);
        }

        private static async UniTask WaitAllInitialApplyAsync(IObjectResolver resolver)
        {
            var targets = resolver.Resolve<IReadOnlyList<IInitialEventApplyWaitTarget>>();
            var waits = targets.Select(target => (target, task: target.WaitForInitialApplyAsync().Preserve())).ToList();
            var allApplied = UniTask.WhenAll(waits.Select(wait => wait.task));

            // 5秒未完了で詰まっている対象を顕在化し、適用待機自体は継続する
            // Surface targets stuck past five seconds while continuing to wait for their application
            // 対象タスクはWhenAllで一度だけawaitする。警告側でも待つとUniTaskの二重await例外になる
            // Await the targets once through WhenAll; awaiting them again in the warning path throws UniTask's double-await error
            WarnStuckTargetsAsync().Forget();
            await allApplied;

            #region Internal

            async UniTaskVoid WarnStuckTargetsAsync()
            {
                await UniTask.Delay(TimeSpan.FromSeconds(5));

                // 未完了(Pending)だけを並べる。faultedは例外として上がるので警告に載せない
                // List only Pending targets; faulted ones surface as exceptions instead
                var pending = string.Join(", ", waits.Where(wait => wait.task.Status == UniTaskStatus.Pending).Select(wait => wait.target.GetType().Name));
                if (pending.Length == 0) return;
                Debug.LogWarning($"[MainGameInitializationFinalizer] 初期イベント適用が未完了のまま待機中: {pending}");
            }

            #endregion
        }
```

- [ ] **Step 7: コンパイルとフルブート回帰テストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: Error 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapVeinOutcropAndRangeView|TerrainCacheFetch|TerrainVisualCacheReuse|PlaytestBootEnvironment"`
Expected: 全件PASS（EditModeInPlayingTestがFinalizer新順序のフルブートを通す）

- [ ] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/Common/IInitialEventApplyWaitTarget.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapVein/MapVeinObjectDatastore.cs \
  moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs \
  moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs
git commit -m "refactor: 初期化待機をUniTask interfaceへ統一し露頭生成を明示呼び出し化する (ADR#15)"
```

---

### Task 6: 最終ブランチレビュー（必須クロージング）

- [ ] **Step 1: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

対象はTask 1〜4の全コミット。4カテゴリcontextにはspec ADR#11〜#14を出所ラベル付きで引用する。

- [ ] **Step 2: レビュー指摘の修正適用後、全変更をコミットする**

---

## 判断記録（ADR）

- 親spec: `docs/plans/map-autogen-world-design.md` の『判断記録（ADR）』#11〜#14（本plan着手前に追記済み。出所はいずれもPR #1104独立レビューダイジェストへのユーザーコメント 2026-08-02）
- planning中の判断:
  - **復元メソッドの置き場**: `ToTerrainTransferMeta()` はワイヤDTO側（Server.Protocol）に置く。Transfer層への `FromLayoutResponse` 案は `Game.MapGeneration → Server.Protocol` の逆向き参照を生むため棄却（依存方向はDTO側配置のみ成立。caller-orchestration reviewer案Bをtype-structureレンズ案で上書き）
  - **`ResponseMapDataMessagePack` コンストラクタのシグネチャ維持**: `(spawn, mapObjects, mapVeins, TerrainTransferMeta, string)` を維持し内部で畳むことで、生成側4呼び出し（サーバー・デバッグ・テスト2）を無改修にする
  - **`IMapVeinRangeView` の2メソッド分離**: `Show(bool)` 単独では距離カリングのフレーム駆動が失われるため、引数なし `ManualUpdate()` を併設（同一呼び出し箇所の `BuildUndoService.ManualUpdate()` と同形の前例一致）。`Show` は即時スイープし、OnExit時の残存ボックスを次フレームへ持ち越さない
  - **D6はログのみ（挙動不変）**: 現行コードはフォールバックでも生成継続しており裁定と一致。追加するのは可視化だけで、throw・分岐は入れない
  - **D1はA+B併用を前提宣言（拒否権つき）としてTask 5に採用**: シミュレーター予測・確信高（出所: preanswer判事 2026-08-02。ユーザー承認後にspec ADR#15の出所を「シミュレーター予測→ユーザー承認」へ更新する）。露頭開始の `resolver.Resolve<MapVeinObjectDatastore>().StartOutcropInstantiation()` は「開始のオーケストレーション」であり待機機構の迂回ではない（待機はWhenAll一本）
  - **その他Critical群（namespace改名・キーリテラル集約・AABBテスト等）は本planのスコープ外**: 別バッチ
  - **plan-review判事の適用（2026-08-02）**: Task 1の波及漏れ2テスト（GetMapDataProtocolTest.cs・GetMapDataTerrainChunkTest.cs）をFiles/Step 7/Step 8/Step 9へ追加。Task 4に探査窓2000f→1500fの差の注記を追加
