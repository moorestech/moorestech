# マップ自動生成のワールド組み込み設計

作成: 2026-07-23 / ステータス: 設計ドラフト（調査完了・実装未着手）

## ゴールと決定事項

- ゲーム内でseedを変えるたびに別マップが遊べるようにする
- 生成器は `TmpUnityPjt/MapMaking` の `Assets/MapGenerator/`（fBm+Burstパイプライン、Stage6鉱脈配置・SpawnRegionFinder実装済み）を移植して使う
- **生成はワールド新規作成時に1回だけ実行し、結果（map.json相当＋地形データ）をワールドデータとして永続化する。以後の起動は保存物のロードのみ**
  - seedからの毎回再生成は不採用（生成アルゴリズム変更で既存ワールドと不整合になるため）
  - エディタ事前ベイク（手動配置の自動化のみ）も不採用（seed別マップが遊べないため）
- サーバーが権威。クライアントはハンドシェイクでマップ全量を受け取り実行時構築する

## 現状の前提（調査結果の要点）

- サーバーは `MoorestechServerDIContainerGenerator.Create()` で `ServerDataDirectory/map/map.json` を読み `MapInfoJson` をDI登録。`MapObjectDatastore` / `ItemMapVeinDatastore` / `FluidMapVeinDatastore` / `WorldSettingsDatastore` はそれを受け取るだけ
- セーブは `~/Library/Application Support/moorestech/saves/save_1.json` 固定の単一ファイル。ワールド選択UIは無く「セーブが無ければ `WorldLoaderFromJson.WorldInitialize()` で新規初期化」
- シングルプレイはクライアントと同一Unityプロセス内で `ServerStarter`（MonoBehaviour）がサーバーを起動。引数は `InitializeProprieties.CreateLocalServerArgs`（現状常に空）
- クライアントの `MainGame.unity` は `Environment.prefab` に地形（Unity Terrain＋Gaia製TerrainData）と木・石・ブッシュ（`MapObjectGameObject` 2011個、instanceIdベイク済み）が静的に焼き込まれている
- ハンドシェイクは `va:initialHandshake` ＋並列7プロトコルの束（`VanillaApiWithResponse.InitialHandShake()`）。mapObjectは `va:mapObjectInfo` で id/破壊/HP のみ送信（座標・GUIDはクライアントシーンにベイク前提）
- 大容量バイナリのネットワーク転送前例は無し
- map.jsonはv8形式（`itemMapVeins`/`fluidVeins`分離）が現行ローダー対応。v5-v7の`mapVeins`単一配列は非互換

## 1. ワールドデータのディレクトリ構成

現行の「単一 save_1.json」から「ワールド＝ディレクトリ」へ移行する。

```
~/Library/Application Support/moorestech/saves/
└── world_1/
    ├── world.json          # ワールドメタ: seed, generatorVersion, mapMode, 作成日時
    ├── map.json            # 生成結果（現行v8形式そのまま: spawn/mapObjects/itemMapVeins/fluidVeins）
    ├── terrain/
    │   ├── height_0_0.r16  # タイルごとのハイトマップ（16bit raw、タイル数・解像度はworld.jsonに記載）
    │   └── biome_0_0.bin   # バイオームインデックスマップ（1byte/セル）
    ├── cache/              # 【削除可能】派生データのキャッシュ。消しても次回起動時に再構築される
    │   ├── README.txt      # 「このディレクトリは削除可能」の旨を明記したファイルを生成時に置く
    │   └── ...             # クライアントが再構築したalphamap・草花density・ベイク済みTerrainData等
    └── save.json           # 従来のsave_1.json相当（差分セーブ、形式無変更）
```

- `world.json` の `mapMode`: `generated`（seed生成）| `template`（既成マップをコピー。現行v8手作りマップやテスト用マップはこちら）
- テクスチャ配分（alphamap）や草花densityは権威データとしては保存しない。見た目はクライアントが `height+biome＋バイオームプリセット` から決定論的に再構築し、**再構築結果は `cache/` にキャッシュとして保持する**（2回目以降の起動を高速化）。`cache/` は削除可能であることをREADME.txtで明示し、欠損時は無条件に再構築する（キャッシュ有無でゲームプレイが変わってはならない）
- リモート接続クライアントはワールドディレクトリを持たないため、同等のキャッシュをクライアントローカル（`GameSystemPaths` 配下の `cache/worlds/<worldId>/`）に置く。受信したterrainチャンクもここにキャッシュする
- 永続化規約への準拠: `world.json`/`map.json` は可読JSON・GUID保存（`mapObjectGuid`/`veinItemGuid`/`veinFluidGuid`。揮発intのIDは保存しない）。`terrain/` のみ画像相当の生データ（数百万セルの高さ・バイオーム値）でありJSON化は非現実的なため、規約の例外としてバイナリを許容する。バイオーム値は生成コード定義の安定enum（マスタのロード順採番ではない）なのでintシリアライズ可
- 開発フェーズのため旧 `save_1.json` からの移行・互換対応は行わない（新形式で作り直す）
- CLIは `--saveFilePath` を `--worldDirectory` に置き換え（`SaveJsonFilePath` → `WorldDirectoryPath`）。テストコードの隔離セーブパス指定も同様に追従

## 2. MapGeneratorコアの移植（新アセンブリ Game.MapGeneration）

`moorestech_server/Assets/Scripts/Game.MapGeneration/` として移植する。

- 移植対象: `TerrainGenerator` のデータ生成部（Stage1-2分類/高さ、Stage3-4木・オブジェクト配置、Stage6鉱脈、SpawnRegionFinder）＋ `Presets/` のConfig群
- **Unityシーン非依存にする**: `TerrainApplier`（TerrainData適用・プレハブInstantiate）は移植しない。出力は純データ:
  - `MapInfoJson`（そのまま既存DTOを出力形式に使う）
  - ハイトマップ `float[]`／バイオームマップ `byte[]`（タイルごと）
- Burst/Collections/Mathematics はサーバープロジェクトのmanifestに追加（クライアントは既存確認）
- **鉱脈の変換**: Stage6のクラスタ（中心＋メンバー座標）→ クラスタごとのAABBを整数グリッドにスナップして `ItemMapVeinInfoJson` 化。地表の鉱石見た目プレハブ相当は `mapObjects` として出力（地表採取と地中鉱脈採掘の2系統に対応）
- **GUIDマッピング**: `OreEntry`/`ObjectEntry`/`TreePrototypeEntry` に `mapObjectGuid`/`veinItemGuid`/`veinFluidGuid` フィールドを追加（マスターデータのGUIDを直接持たせる）。プレハブ参照はクライアント表示用マッピングに分離
- **流体鉱脈**: `OreEntry` を汎化し `FluidVeinEntry` を追加（配置ロジック共通、出力先が `fluidVeins`）
- instanceId採番は生成時に確定し map.json に書き込む（現行 `MapExportAndSetting` の採番処理と同等）

## 3. サーバー側改修

### 3-0. WorldDataDirectory（ワールドデータ統括クラス、Game.Paths）

ワールドディレクトリ内の全ファイル配置（world.json / map.json / terrain/ / cache/ / save.json）の真実源となる値オブジェクト。プロビジョナ・DIコンテナ・セーブ/ロード・P2以降のプロトコル実装はすべてこのクラス経由でパスを得る（パスの文字列連結を各所に散らさない）。既存の `SaveJsonFilePath` はこのクラスに置換・吸収する。プロビジョニングは一時ディレクトリ（`<root>.provisioning`）に書き切ってから `Directory.Move` で確定し、world.jsonをコミットマーカーとして最後に書く（中断による壊れたワールドの残留を防ぐ）。

### 3-1. ワールドプロビジョニング（新設）

DIコンテナ構築より前に実行する前処理。`ServerInstanceManager.Start()` で `StartServerSettings` 解析後、`Create()` 呼び出し前に挟む:

```
WorldProvisioner.EnsureWorld(worldDirectory, settings):
  world.json が存在 → 何もしない（既存ワールド）
  存在しない → 新規作成:
    mapMode=generated → MapGeneratorコアをseedで実行 → map.json + terrain/ + world.json を書き出し
    mapMode=template  → テンプレート（ServerDataDirectory/map/ 等）から map.json をコピー
```

`MapInfoJson` のロードはDIコンテナ生成の冒頭で行われるため、この順序でロード経路（`MapInfoJson`→Datastore群）は完全無改修で済む。map.jsonのパス解決だけ `worldDirectory/map.json` に変更。

### 3-2. seedの保存先

`world.json` を真実源とする（生成前に必要なためセーブファイルには置けない）。`WorldSettingJsonObject` へのSeed追記は表示用の複製として任意。

### 3-3. クライアント早期DI初期化の依存解消（要注意ポイント）

`InitializeScenePipeline` はマスターデータロード目的でクライアント側でも `Create()` を呼ぶが、ここで map.json を読んでいる。リモート接続時クライアントにはワールドディレクトリが無いため、この経路の `MapInfoJson` 依存を外す（空登録 or マスターデータ初期化とマップ初期化の分離）。ローカル同居時はサーバー本体のDIが権威なので問題にならない。

## 4. プロトコル設計

規約「1ドメイン1プロトコル・リクエスト内enumでMode分岐」に従い、新設は1本。既存の `va:mapObjectInfo`（状態同期）と `MapObjectUpdateEventPacket` は無改修で継続使用。

### `va:mapData`（GetMapDataProtocol、新設）

| Mode | リクエスト | レスポンス |
|---|---|---|
| `Layout` | なし | spawn / mapObjects全量（instanceId, mapObjectGuid, x,y,z）/ itemMapVeins / fluidVeins（GUID＋AABB）。実測ベースで数百KB以下、単発送信 |
| `TerrainChunk` | tileX, tileY, chunkIndex | ハイトマップ＋バイオームマップの圧縮断片（byte[]）。分割転送し、総チャンク数は `Layout` 応答のメタに含める |

- 実装は3点セット（MessagePack定義／`PacketResponseCreator` 登録／`VanillaApiWithResponse` メソッド）＋ creating-server-protocol スキルに従う
- `VanillaApiWithResponse.InitialHandShake()` の並列取得束に `Layout` を追加。`TerrainChunk` はハンドシェイク後〜シーンアクティブ化前のロード画面中に順次取得
- veinのAABBはクライアント表示不要だが、デバッグGizmo表示のため `Layout` に含めて送る

## 5. クライアント側改修

1. **mapObjectの実行時生成**: `MapObjectGameObjectDatastore` をシーン事前ベイク（SerializeFieldリスト）方式から、`Layout` 応答の（guid, 座標, instanceId）で実行時Instantiateする方式へ変更。`mapObjectGuid`→プレハブのマッピング表（ScriptableObject、Addressables参照）を新設。2011個規模の起動時スパイク対策としてフレーム分散Instantiate。以降の状態同期（instanceId突き合わせ・破壊/HP反映）は現行ロジックをそのまま使う
2. **地形の実行時構築**: 受信した height+biome から `TerrainData` を実行時生成（MapMakingの `TerrainApplier` の適用部を移植）。テクスチャ・ディテール（草花）はバイオームプリセット（`BiomeTextureConfig`/`BiomeDetailConfig`、クライアント側アセット）から決定論生成
3. **Environment.prefab の再編**: ベイク済みTerrain・木・石・ブッシュを撤去し、実行時構築のマウントポイント（EnvironmentRoot配下）に置き換える。テンプレートマップ（現行v8手作りマップ）も同じ実行時構築経路に載せる（map.json＋既存TerrainDataから構築）ことで経路を1本化

## 6. 実装フェーズ

| フェーズ | 内容 | 検証 |
|---|---|---|
| P1 | Game.MapGeneration移植＋WorldProvisioner。サーバー単体でseed→map.json生成→既存ローダー起動 | 生成map.jsonのロード・Datastore構築のユニットテスト、生成時間実測 |
| P2 | `va:mapData` Layout追加＋クライアントmapObject実行時Instantiate化（地形は暫定で既存ベイクのまま） | ハンドシェイク〜採取動作のプレイテスト |
| P3 | TerrainChunk転送＋地形実行時構築＋Environment.prefabベイク撤去 | seed違いで別地形が表示されること |
| P4 | ワールド選択・新規作成UI（seed入力）＋複数ワールドディレクトリ対応（UIはWeb側） | 新規作成→別マップ→再起動で同一マップ |
| P5 | 流体鉱脈エントリ追加、テンプレートマップ（現行v8手作りマップ）の共存整備 | template/generated両モードの起動確認 |

## 7. リスク・未決事項

- クライアント早期DIの `MapInfoJson` 依存解消の具体方法（3-3）は実装時に要精査
- 生成時間の実測未了（Burst済みなので新規作成時の1回なら許容見込み）
- 木の見た目とmapObject当たりの一致: 木もmapObjectとしてサーバー生成データに含めるため不一致は原理上発生しない（クライアントは受けた座標に描くだけ）
- マスターデータ変更（mapObjectGuid廃止等）による既存ワールドとの不整合は現行の静的map.jsonと同等のリスクであり、本設計で悪化しない（GUID保存のためマスタの並び順・増減では化けない）
- ワールドUIはWeb移行完了済みのため P4 のUIはWeb側で実装
