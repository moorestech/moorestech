---
name: reproducing-heightmaps
description: |
  既存のハイトマップ画像（PNG/TIF）を参考に、MapGeneratorのバイオームSampleHeightアルゴリズムと
  パラメータをプロシージャルに再現するワークフロー。

  Use When:
  - 「この画像に似せて」「このハイトマップを再現して」などのリクエスト
  - 特定のバイオームの地形パターンを既存画像に近づけたい
  - Gaiaスタンプや外部ハイトマップを参考にバイオームを調整したい
  - バイオームのSampleHeightアルゴリズムを根本的に改善したい
---

# 既存ハイトマップ再現ワークフロー

## 前提

- BiomeHeightmapExporter（`Tools > MapGenerator > Export Biome Heightmap`）で個別バイオームPNG出力可能
- 参考プロジェクト `/Users/katsumi/WebstormProjects/MapGenerator/` にTS版アルゴリズム実装あり
- Unityのシリアライズ値はコードのデフォルト変更では更新されない → `uloop execute-dynamic-code` で直接設定

## ワークフロー

### 1. 参考画像の視覚分析

参考画像を `Read` で開き、以下を特定:
- **スケール**: 特徴が画像の何割を占めるか → frequency
- **コントラスト**: 明暗の分布（二峰性 or 均一） → exponent / S曲線
- **エッジ**: 境界の鋭さ → ridged noise / edgeMask
- **パターン**: 有機的流れ or 規則的 → ドメインワープ有無
- **テクスチャ**: プラトー面の粗さ → persistence / octaves

### 2. 現在の実装と参考実装を読む

対象バイオームの `SampleHeight` + `BiomeConfig` を読む。TS版の該当アルゴリズムも確認:
- `src/core/algorithms/` — broken-lands, ridged, voronoi 等
- `src/core/world/biome-shaping.ts` — バイオーム別高さ変形
- `src/core/noise-helpers.ts` — fbm, fbmRaw, ridgedNoise

### 3. アルゴリズム設計

視覚分析に基づき技法を選択:

| 視覚特徴 | 技法 | 実装 |
|---|---|---|
| 有機的な流れ | ドメインワープ | `SampleFBmRaw` で座標変位 |
| 鋭い稜線 | Ridged multifractal | `SampleRidged` |
| 渓谷/チャネル | abs-noise min | `abs(Perlin-0.5)*2` ループ |
| プラトー平坦化 | smoothstep S曲線 | `h*h*(3-2*h)` ブレンド |
| コントラスト強調 | べき乗ガンマ | `Pow(h, exponent)` |
| 境界稜線 | edgeMaskベース加算 | `Pow(4*h*(1-h), 1.5)` |

**処理順序が重要**: edgeMaskはS曲線前のterrainから計算し、リッジ加算はS曲線/ガンマの後に行う。逆だと稜線が鈍化する。

### 4. 反復チューニング（核心）

`uloop execute-dynamic-code` でパラメータを直接設定してエクスポート:

```csharp
var go = UnityEngine.GameObject.Find("MapGenerator");
var facade = go.GetComponent<MapGenerator.MapGeneratorFacade>();
var gc = facade.config.<biome名>;
gc.frequency = 0.0007f;   // ノイズ周波数（小さい=大きな特徴）
gc.domainWarpStrength = 750f;  // ワープ変位量(m)
gc.exponent = 2.0f;       // コントラストガンマ（>1で暗部強調）
// ... 他のパラメータ ...
UnityEditor.EditorUtility.SetDirty(go);
var path = MapGenerator.Pipeline.Diagnostics.BiomeHeightmapExporter.Export(
    MapGenerator.Pipeline.Biomes.BiomeType.<Type>, facade.config, 512);
return $"Exported: {path}";
```

`Read` で生成画像と参考画像を並べて比較 → 差分分析 → 調整 → 再エクスポート。目安5〜15回。

### 5. パラメータ調整指針

| 問題 | 調整 |
|---|---|
| 特徴が小さい/大きい | frequency ↓/↑ |
| 滑らかすぎ/ノイジー | persistence ↑↓, octaves ↑↓ |
| コントラスト不足/暗すぎ | exponent ↑↓, plateauFlatten ↑↓ |
| 有機感不足/渦巻きすぎ | domainWarpStrength ↑↓, iterations ↑↓ |
| 稜線が太い/見えない | edgeMask累乗 ↑, ridgeBlend ↑ |
| 等高線アーティファクト | canyonDepth → 0, valleySharpness ↓ |

### 6. 完了

デフォルト値をコード上で最終パラメータに更新。外部監査（`gemini -p`）で確認。

## 注意

- **Perlin vs Simplex**: Unity Perlin [0,1], TS Simplex [-1,1]。変換: `abs(Perlin-0.5)*2` ≈ `abs(Simplex)`
- **オフセット配列**: 各ノイズチャネルに専用区間を割り当て、`RequiredNoiseOffsetCount` を更新
- **プロシージャル純粋性**: ハードコード座標→出力マッピング禁止。すべて数学的関数から導出
