# デバッグ環境Otherの固定地形25枚を再生成する手順

`GeneratedTerrain5x5.prefab` が参照する25枚のTerrainDataは、1枚あたり150MB前後でGitHubの100MB制限を超えるため
`moorestech-client-private/.gitignore` の `/GeneratedTerrains/` で意図的に追跡対象外になっている。
つまりcloneしただけでは25枚は存在せず、Terrainは `terrainData == null` のまま何も描画しない。
各自の環境で下記の手順を1回実行して手元に生成する。生成物は約3.6GB。

## 前提

- `TmpUnityPjt/MapMaking` の `Assets/PersonalAssets/` に `moorestech-client-private` が配置されていること
  （空だとBiome参照が238件未解決になり生成が破綻する）。`cp -Rc` でクローンすればAPFSのcopy-on-writeにより実容量は増えない
- `TmpUnityPjt/MapMaking/.uloop/settings.permissions.json` で `dynamicCodeSecurityLevel: 2` になっていること

## 手順

### 1. MapMakingで25チャンクを生成する

新規シーンに `InfiniteTerrainManager` を置き、`Presets/DefaultConfig.asset` を割り当てて `RegenerateAllChunks()` を呼ぶ。
`baseColorConfig` 等のフィールド代入はEditModeではシリアライズされないため、必ず `SerializedObject` 経由で書き込む。

```csharp
var cfg = AssetDatabase.LoadAssetAtPath<MapGenerator.Pipeline.TerrainGenerationConfig>("Assets/MapGenerator/Presets/DefaultConfig.asset");

// プリセットは generateDetail/generateObject が0のままだが、ベイク済みObjects/Oresと揃えるため両方1で生成する
// The preset ships both flags off, but bake them on to stay consistent with the committed Objects/Ores prefabs
var so = new SerializedObject(cfg);
so.FindProperty("generateObject").boolValue = true;
so.FindProperty("generateDetail").boolValue = true;
so.ApplyModifiedPropertiesWithoutUndo();

var mgr = new GameObject("InfiniteTerrainManager").AddComponent<MapGenerator.InfiniteTerrainManager>();
var mso = new SerializedObject(mgr);
mso.FindProperty("baseConfig").objectReferenceValue = cfg;
mso.ApplyModifiedPropertiesWithoutUndo();

mgr.RegenerateAllChunks();
```

seed 196 / 1000m四方 / 5×5グリッドで `Chunk_-2_-2` 〜 `Chunk_2_2` の25個ができる。所要3分強、樹木76,815本。
`uloop execute-dynamic-code` のCLIタイムアウト180秒を超えるが、Unity側は走り続けるのでポーリングで完了を待つ。
生成直後のシーンはTerrainDataを埋め込むため保存すると3.9GBになる。保存せず次へ進むこと。

### 2. アセットとして書き出す

メニュー `Tools/MapGenerator/Export Scene-Only Terrain (Run All)` を実行する。
`Assets/MapGenerator/TerrainData/SceneOnly/` に `InfiniteTerrainManager_Chunk_{x}_{z}_TerrainData.asset` が25枚出る。

### 3. クライアントへ配置する

```bash
cp -Rc TmpUnityPjt/MapMaking/Assets/MapGenerator/TerrainData/SceneOnly/*.asset \
       moorestech_client/Assets/PersonalAssets/moorestech-client-private/GeneratedTerrains/
```

`.meta` はコピーせずUnityに生成させる。

### 4. プレハブの参照を張り直す

再生成でGUIDが変わるため、`GeneratedTerrain5x5.prefab` の25個の `Terrain` と `TerrainCollider` を名前で対応付けて繋ぎ直す。
`Terrain_{x}_{z}` ↔ `InfiniteTerrainManager_Chunk_{x}_{z}_TerrainData.asset`。

```csharp
var root = PrefabUtility.LoadPrefabContents(prefabPath);
foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
{
    var coord = terrain.gameObject.name.Substring("Terrain_".Length);
    var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataFolder + "/InfiniteTerrainManager_Chunk_" + coord + "_TerrainData.asset");
    var so = new SerializedObject(terrain);
    so.FindProperty("m_TerrainData").objectReferenceValue = data;
    so.ApplyModifiedPropertiesWithoutUndo();
    // TerrainColliderにも同じ参照があるので両方書き換える
    // TerrainCollider holds the same reference, so write both
}
PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
```

### 5. 確認

デバッグメニューの環境をOtherにしてPlayModeへ入り、`Terrain_*` が25個・`terrainData != null` になることを見る。

## 注意

生成器はプレハブのベイク時（2026-06-02/04）から変化しており、同じseedでも当時と同一の地形にはならない。
オブジェクト配置数は1,986→8,295、鉱脈は2,635→2,249。樹木・岩の後処理がheightmapとsplatmapを書き換えるため地表も別物になる。
ベイク済みの `MapGenerator_Objects.prefab` / `MapGenerator_Ores.prefab` とは厳密には整合しない。
