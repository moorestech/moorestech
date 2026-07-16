## Project Overview

git worktree頻用のため、最初に必ず`pwd`で現在ディレクトリを確認すること。

Unity 6 (6000.3.8f1) terrain/map-making project using **MicroVerse** (v1.7.0) for procedural terrain generation with **URP** (17.3.0). The project combines MicroVerse stamp-based terrain tools, BK Pure Nature environment assets (13 biome variations), Gaia terrain stamps, and Kripto KWS2 water system.

## uLoopMCP Integration

This project has **uLoopMCP** installed for AI-driven Unity Editor automation. 15 skills are available under `.claude/skills/`. When the uLoop server is running in Unity (Window > uLoop), you can use skills like `/uloop-compile`, `/uloop-get-logs`, `/uloop-run-tests`, `/uloop-screenshot`, `/uloop-get-hierarchy`, etc.

**Current security settings** (`.uloop/settings.permissions.json`):
- `enableTestsExecution`: false
- `allowMenuItemExecution`: false
- `allowThirdPartyTools`: false
- `dynamicCodeSecurityLevel`: FullAccess (動的コード実行を積極的に使用すること)

**Typical AI-driven workflow:**
```
/uloop-compile → check errors → fix code → /uloop-compile → /uloop-get-logs → /uloop-run-tests
```

**動作確認ワークフロー:**
コード変更後の動作確認は `uloop execute-dynamic-code` を積極的に使うこと。**ユーザーと同じ検証ルートを使うこと。** `FindFirstObjectByType` は複数インスタンスがある場合に意図しないオブジェクトを取得する危険があるため、`GameObject.Find("オブジェクト名")` で明示的に指定する。MapGenerator の場合:
```bash
# マップ再生成（MapGeneratorオブジェクトのFacadeを使用）
uloop execute-dynamic-code --code '
var go = UnityEngine.GameObject.Find("MapGenerator");
var facade = go.GetComponent<MapGenerator.MapGeneratorFacade>();
var terrains = facade.CollectTerrains();
facade.Generate();
foreach (var t in terrains)
    UnityEditor.EditorUtility.SetDirty(t.terrainData);
return $"Done: {terrains.Length} terrains";
'
# スクリーンショットで結果確認
uloop screenshot --window-name Scene
# 外部監査（codex-audit.mjs）— 初回は新規セッション、2回目以降は --session で再利用
node tools/codex-audit.mjs <screenshot_path> --ask "確認観点の指示"
# → stderr に「--session <UUID> を指定してください」と表示される
# 以降同一セッションで呼び出す場合:
node tools/codex-audit.mjs <screenshot_path> --ask "確認観点の指示" --session <UUID>
```

To launch Unity: `/uloop-launch` or `uloop launch`

**uloopタイムアウト時の対応:**
uloopコマンドが30秒以上タイムアウトする場合、Unityが無限ループまたはハング状態にある可能性がある。`pkill -f Unity` でプロセスを強制終了し、コードに無限ループがないか確認すること。重い処理（大量データの取得・比較）をdynamic codeで実行するとハングの原因になるため、軽量な操作に分割すること。

## Architecture

### Rendering & Terrain Pipeline
- **URP** with deferred rendering, linear color space
- **MicroVerse** packages: core, vegetation, objects, splines, ambiance (all in `Packages/`)
- Terrain stamps in `Assets/Procedural Worlds/Gaia/Stamps/`
- Height maps in `Assets/50 Free .PNG Heightmaps.../`
- Rendering settings: `Assets/Settings/PC_RPAsset.asset` (PC), `Mobile_RPAsset.asset` (mobile)

## Key Packages (manifest.json)

MicroVerse ecosystem, URP 17.3.0, Burst 1.8.27, Input System 1.18.0, Cinemachine 2.10.5, Test Framework 1.6.0, uLoopMCP (Git URL from GitHub)

## MicroVerse に関する質問への対応

MicroVerse の機能・設定・使い方について質問されることがあります。回答する際は以下を徹底してください：

1. **一次情報を確認する** — `docs/` 配下の公式ドキュメント、`Packages/com.jbooth.microverse*/` 内の実際のコード（設定値・挙動・関数シグネチャなど）を必ず参照し、事実に基づいた回答を行う
2. **推測を明示する** — コードやドキュメントから確定的な情報が得られない場合は、回答に「推測を含みます」「ドキュメントで確認できませんでした」等の注記を付ける
3. **憶測で回答しない** — 確認できない機能やパラメータについて、あたかも確定情報であるかのように回答しない



## 開発方針

**MapGenerator をメインの開発対象とする。** MicroVerse は既存アセットとして残すが、今後の地形生成機能の開発・拡張は MapGenerator モジュールで行う。

## MapGenerator

`Assets/MapGenerator/` にある自作のプロシージャルマップ生成システム。fBm + Perlin ノイズで Unity Terrain のハイトマップを生成し、複数タイルのシームレスタイリングに対応。エントリポイントは `MapGeneratorFacade`（MonoBehaviour）、パラメータは `MapGeneratorConfig` で管理。カスタムインスペクタから Generate ボタンで実行可能。

### 設定ファイル構成

**グローバル設定:**
- `Presets/DefaultConfig.asset` (`TerrainGenerationConfig`) — 解像度・シード・大陸ノイズ・バイオーム分布・共通テクスチャ・生成レイヤー切替

**バイオームプリセット:** `Presets/Biomes/{Biome}.asset`
各バイオームの BiomeConfig（ScriptableObject）。ハイトマップStage 1-8 + Visual 1-4 の構成。

| バイオーム | プリセット | Config実装 | BKパック |
|---|---|---|---|
| Grassland（草原） | `Grassland.asset` | `Biomes/Grassland/GrasslandBiomeConfig.cs` | `PureNature_Mountains` |
| Forest（森林） | `Forest.asset` | `Biomes/Forest/ForestBiomeConfig.cs` | `PureNature_Redwood` |
| Savanna（サバンナ） | `Savanna.asset` | `Biomes/Savanna/SavannaBiomeConfig.cs` | `PureNature_Savanna` |
| Desert（砂漠） | `Desert.asset` | `Biomes/Desert/DesertBiomeConfig.cs` | `PureNature_Oasis` |
| Mesa（メサ） | `Mesa.asset` | `Biomes/Mesa/MesaBiomeConfig.cs` | `PureNature_MesaDesert` |
| Alpine（高山） | `Alpine.asset` | `Biomes/Alpine/AlpineBiomeConfig.cs` | （未設定） |
| Jungle（ジャングル） | `Jungle.asset` | `Biomes/Jungle/JungleBiomeConfig.cs` | `PureNature_Jungle` |
| Woods（林） | `Woods.asset` | `Biomes/Woods/WoodsBiomeConfig.cs` | `PureNature`（base） |

パスはすべて `Assets/MapGenerator/Pipeline/` からの相対。BKアセットルートは `Assets/PersonalAssets/moorestech-client-private/BK/`。

**Visual 1-4 の設定クラス（全バイオーム共通構造）:**

| Inspector名 | フィールド名 | 設定クラス | ファイル |
|---|---|---|---|
| テクスチャ設定 | `textureConfig` | `BiomeTextureConfig` | `Config/BiomeTextureConfig.cs` |
| 樹木配置 | `treePlacement` | `TreePlacementConfig` | `Config/TreePlacementConfig.cs` |
| オブジェクト設定 | `objectConfig` | `BiomeObjectConfig` | `Config/BiomeObjectConfig.cs` |
| ディテール設定 | `detailConfig` | `BiomeDetailConfig` | `Config/BiomeDetailConfig.cs` |

**Visual 設定内のネスト構造:**

- `TreePlacementConfig` → `TreePrototypeEntry[]`（`Config/TreePrototypeEntry.cs`）、`TreeDensityConfig`、`UnderstoryConfig`、`RockProximityTreeConfig`
- `BiomeObjectConfig` → `ObjectClusterEntry[]`（`Config/ObjectClusterEntry.cs`、Primary→Secondary→RubblePatchの3層）、`ObjectEntry[]`
- `BiomeDetailConfig` → `DetailEntry[]` → `DetailPrototypeConfig`（`Config/DetailPrototypeConfig.cs`）

### バイオーム別ハイトマップエクスポート

各バイオームの `SampleHeight()` 出力を 512x512 グレースケール PNG として書き出す診断機能。バイオームのパラメータ調整時にノイズパターンを視覚的に確認するために使う。

- **MenuItem:** `Tools/MapGenerator/Export Biome Heightmap/{BiomeType}`（Grassland, Forest, Savanna, Desert, Mesa, Alpine, Jungle の7種）
- **実装:** `Editor/BiomeHeightmapExportMenu.cs` → `Pipeline/Diagnostics/BiomeHeightmapExporter.cs`
- **出力先:** `Assets/MapGenerator/Export/Biome_{BiomeType}.png`
- **処理:** シーン上の MapGenerator から config を取得 → バイオーム生成 → 全ピクセルの SampleHeight を呼び出し → min/max 正規化 → PNG 保存



## 回答方針

- パラメータに言及する際は、**Inspector上の日本語ラベル名**（`[Label("...")]`で設定された名前）を必ず含めること。例：「`exponent`（べき乗指数）」「`hillAmplitude`（丘の振幅）」

## コメント方針

MapGenerator のコードには以下の方針でコメントを書く：

- **日本語で、本質的なコメントのみ**。そのコードが「なぜ存在するのか」の意図を解説する。処理内容をそのまま言い換えた無意味なコメントは書かない
- **3〜5行に1回**の頻度。コメント行数は基本1行、多くて2行
- **依存先・呼び出し先を含めた文脈**で書く。そのコード単体ではなく、どこから呼ばれるか・何に使われるかを踏まえた見通しの良いコメントにする
- クラスの `<summary>` にはそのクラスの役割とパイプライン内での位置づけを書く

## 重要事項

- **コード変更よりパラメータ調整を優先すること。** 新機能の追加やアルゴリズムの変更は最終手段。まずは既存のパラメータ（ノイズ設定、スケール、密度、配置モード等）の組み合わせで目標に近づけること。コード変更は、パラメータだけでは物理的に実現不可能と判断できた場合の最後の詰め・最終調整として行う。
- **ユーザーに提示するすべての事実・原因分析は、提示前に必ずコードやデータを調べて裏付けること。** 推測や仮説を事実として提示してはならない。「バイオーム境界だと思います」のような未検証の推測を断定的に述べない。調べればすぐわかることを調べずに回答するのは信頼を損なう。原因分析は必ず①データ確認→②仮説立案→③検証の順で行い、検証済みの事実のみ提示すること。
- **スクリーンショット・画像の確認時は必ず外部監査を行うこと。** 直接の目視確認のみで完了としてはならない。以下のフォーマットで呼び出すこと:
  ```bash
  # 初回（新規セッション）
  node tools/codex-audit.mjs <画像パス> [画像パス2 ...] \
    --ask "【評価基準】（何を基準に評価するか）【確認観点】（今回何を確認したいか）"

  # 2回目以降（同一セッション再利用）
  node tools/codex-audit.mjs <画像パス> --ask "..." --session <UUID>
  ```
  `--ask` には **評価基準**（どのバイオームか、どんな地形を期待するか等）と **確認観点**（今回のスクリーンショットで何をチェックするか）の両方を含めること。バイオーム別の基本要件は `tools/audit-requirements.md` に定義済みで新規セッション時に自動挿入される。ユーザーから特別な指示がない限り、常にセッションを使用すること。
- **自身の確認結果と外部監査の結果が矛盾する場合は、外部監査の結果を優先すること。**
- **外部監査でA以外の評価が返った場合は、ユーザーの指示を待たず自主的に修正を開始すること。** A評価になるまで修正→再生成→再監査のループを回す。ユーザーに確認を求めるのはA評価に到達してからでよい。
- **外部監査への相談の有用性**: 監査人にアルゴリズムの概要・現在のパラメータ・修正方針を伝え、改善の方向性を相談することも有効。コードのパスを渡すことで監査人もコードを読み取り、周辺コードを含めてチェックし改善提案をしてくれる。積極的にコードを渡し相談すること。また、設定しているパラメーターも含めて送る事。
- **プロシージャル生成でハードコードされた座標→出力のマッピングを書かないこと。** 「座標(x,y)のときは出力はZ」のようなルックアップテーブルやif分岐は禁止。すべての出力は数学的関数・ノイズ・シミュレーションから導出され
なければならない。特定のサンプル画像に合わせるためにピクセル値を直接埋め込むのはプロシージャル生成の意味を失
う。パラメータ調整で近づけること。

