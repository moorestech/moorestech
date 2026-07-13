---
name: biome-creation
description: |
  MapGeneratorのバイオームにプレハブ（岩石・石・小物・樹木）を配置するワークフロー。
  プレハブ選定→パラメータ設定→再生成→スクリーンショット検証→外部監査の一連の流れを実行する。

  Use When:
  - バイオームに岩石・石・小物を配置したい
  - バイオームの樹木・植生プレハブを設定したい
  - 参考画像に合わせてバイオームのビジュアルを調整したい
  - 新しいプレハブをバイオームに追加・登録したい
  - バイオームの見た目を作り込みたい
---

# Biome Creation

MapGeneratorバイオームへのプレハブ配置ワークフロー。

## 重要ルール

### Tree vs Object: 配置システムの使い分け

**岩石・石・小物は TreePrototype として登録する。ObjectPlacement は使わない。**

| 配置システム | 用途 | 設定先 |
|---|---|---|
| `treePlacement.prototypes` (TreePrototypeEntry[]) | 樹木、岩石、石、小物 **全般** | バイオームConfig の `treePlacement` |
| `objectConfig.entries` (BiomeObjectConfig) | 崖オブジェクト等 **特殊用途のみ** | バイオームConfig の `objectConfig` |

Unity の Tree システムは LOD・ビルボード・バッチング最適化が効くため、大量配置に適している。
Object 配置は個別 GameObject インスタンスになるため、崖や大型構造物など少数配置にのみ使う。

### 既存パラメータの保護: 指示された変更のみ行う

**指示された内容以外の既存設定項目は絶対に変更しない。**
既存の樹木・テクスチャ・ハイトマップ等のパラメータは、すでにチューニング済みで問題ないと判断されたもの。

**以下はすべて禁止（直接・間接を問わない）:**
- 樹木エントリの `heightWeightCurve` や他のパラメータを変更する
- 新規追加する石エントリに `heightWeightCurve` を設定して相対的に樹木の選択確率を下げる
- entry複製やweight操作で既存エントリの選択比率を間接的に変える

`SelectPrototype` は全プロトタイプの重みの合計に対する比率で選択する。
石エントリの重みを上げれば、樹木エントリの値を変えなくても樹木の選択確率は相対的に低下する。
**これは樹木パラメータの変更と同義であり、禁止対象。**

石の密度を上げたい場合は、石エントリの追加・パラメータ調整のみで行い、
樹木の選択確率に影響を与えない方法を取ること（例: クラスタリングノイズの調整、フィルタの調整など）。

### 検証: 個数ではなくスクリーンショットの視覚的密度を重視

配置後の確認で「770個配置されました」のような **個数は意味がない**。
マップが大きければ大量のオブジェクトが配置されるが、実際の密度が低ければ問題。

**正しい検証フロー:**
1. 再生成後、Scene View カメラを **石が見える距離** まで近づける
2. `uloop screenshot --window-name Scene` でスクリーンショット撮影
3. 参考画像がある場合は外部監査で比較（`node tools/codex-audit.mjs`）
4. 視覚的密度・分布パターン・スケール感が参考画像と一致するか確認

## ワークフロー

### Step 1: プレハブの調査

対象ディレクトリのプレハブ一覧を確認し、用途別に分類する。

```bash
ls <prefab_directory>/
```

分類例: Boulder（大岩）、Stone（中石）、Pebble（小石）、Cliff（崖→Object用）

### Step 2: バイオームConfigの構造確認

`uloop execute-dynamic-code` で現在の設定を確認する。

```csharp
var go = UnityEngine.GameObject.Find("InfiniteTerrainManager");
var mgr = go.GetComponent<MapGenerator.InfiniteTerrainManager>();
var cfg = mgr.baseConfig;
// バイオーム例: cfg.grassland, cfg.forest, cfg.desert, etc.
var biome = cfg.grassland;
var protos = biome.treePlacement.prototypes;
// 現在のプロトタイプ数と設定を確認
```

### Step 3: プレハブのロードと設定

`AssetDatabase.LoadAssetAtPath` でプレハブをロードし、TreePrototypeEntry に設定する。
TreePrototypeEntry の各フィールドの意味・型・推奨値が不明な場合、[references/tree-prototype-params.md](references/tree-prototype-params.md) を読み込む。

```csharp
var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
    "Assets/path/to/Rock.prefab");
```

**既存のプロトタイプ配列を壊さないよう注意。** 追加する場合は既存配列と結合する。

### Step 4: 再生成と検証

```csharp
// 再生成
var go = UnityEngine.GameObject.Find("InfiniteTerrainManager");
var mgr = go.GetComponent<MapGenerator.InfiniteTerrainManager>();
mgr.RegenerateAllChunks();
```

再生成後、**毎回同じカメラ設定**で撮影して比較する:

```csharp
var sv = UnityEditor.SceneView.lastActiveSceneView;
sv.pivot = new UnityEngine.Vector3(150f, 28f, -50f); // 石が密集するエリア
sv.rotation = UnityEngine.Quaternion.Euler(45f, 0f, 0f);
sv.size = 40f; // 石が個別に見える距離
sv.Repaint();
```

1. 上記でカメラを配置エリアに近づける
2. `uloop screenshot --window-name Scene` でスクリーンショット撮影
3. 参考画像と外部監査で比較

### Step 5: パラメータ調整ループ

外部監査でA評価になるまで以下を繰り返す:
- スケール範囲の調整
- 密度（weight / clusterNoise threshold）の調整
- クラスタリングノイズの調整
- sinkの調整（岩が浮いている/埋まりすぎの場合）

## Gotchas

- シーン上のルートオブジェクト名は `InfiniteTerrainManager`（`MapGenerator` ではない）
- configフィールド名は `baseConfig`（`config` ではない）
- `treePlacement.prototypes` を `=` で上書きすると **既存の樹木設定が消える**。必ず既存配列と結合すること
- **既存エントリのパラメータを絶対に変更しない**。既存パラメータはチューニング完了済み
- **既存エントリの選択確率を間接的に変えることも禁止**。`SelectPrototype` は全プロトタイプの重み合計に対する比率で選択するため、新規エントリに `heightWeightCurve` を設定したり、entry を大量複製すると既存樹木の選択確率が相対的に低下する。これは既存パラメータの変更と同義
- **`SetDirty` は変更した実体アセットに対して呼ぶ**。`baseConfig`（DefaultConfig.asset）と `grassland`（Grassland.asset）は別ファイル。バイオームConfigを変更した場合、`SetDirty(cfg.grassland)` のようにバイオームConfig自体をDirtyにしないとディスクに保存されない。`SetDirty(cfg)` だけでは子アセットに変更が反映されない
- 再生成は `mgr.RegenerateAllChunks()` で全チャンク再生成

## プレハブアセットの場所

主要な岩石プレハブ:
- `Assets/PersonalAssets/moorestech-client-private/BK/PureNature_Mountains/Prefabs/Rocks/`
  - Boulder1-3: 大岩
  - Stone1-5, Stone1b-4b: 中〜小石
  - Pebble1-3: 小石
  - Cliff1-8: 崖（**Object配置用**）
