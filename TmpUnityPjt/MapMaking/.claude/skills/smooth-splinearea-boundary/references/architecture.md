# MicroVerse SplineArea アーキテクチャ

> **表示名と内部名の対応**:
> - 表示名（UI）: **境界ブレンド**
> - 内部名（コード）: **Falloff**（`FalloffOverride`, `FalloffFilter`, `splineAreaFalloff` 等）
>
> クラス名・変数名はすべて内部名（Falloff）のまま。UI上の日本語表示のみ「境界ブレンド」を使用。

## コンポーネント関係

```
SplineArea (SplineContainer + SplineArea)
└── Biome Container (FalloffOverride = 境界ブレンドオーバーライド)
    ├── ClearStamp
    ├── HeightStamp
    ├── TextureStamp
    ├── TreeStamp
    ├── DetailStamp
    └── ObjectStamp
```

SplineAreaがSDF（符号付き距離場）を生成し、FalloffOverride（境界ブレンドオーバーライド）がその距離を使って境界ブレンドを計算する。各Stamp（Height/Texture/Tree等）はFalloffOverrideを`GetComponentInParent`で取得して適用する。

## 主要クラス

### SplineArea (`Packages/com.jbooth.microverse.splines/Scripts/SplineArea.cs`)

- `maxSDF: float` - SDF計算の最大距離。この範囲外は計算されない
- `sdfRes: SplinePath.SDFRes` - SDF解像度（k256/k512/k1024/k2048）
- `GetSDF(Terrain t)` - テレインごとのSDFテクスチャを返す

### FalloffOverride (`Packages/com.jbooth.microverse/Scripts/FalloffOverride.cs`)

- `filter: FalloffFilter` - 境界ブレンド（フォールオフ）設定を保持

### FalloffFilter (`Packages/com.jbooth.microverse/Scripts/FalloffFilter.cs`)

FilterType列挙型:
- Global, Box, Range, Texture, **SplineArea**, PaintMask

SplineArea関連フィールド（`#if __MICROVERSE_SPLINES__`内）:
- `splineArea: SplineArea` - 参照するSplineArea
- `splineAreaFalloff: float` - 境界ブレンド距離（フォールオフ距離）
- `splineAreaFalloffBoost: float` - 境界ブレンド範囲の追加拡張

共通フィールド:
- `easing: Easing` - 境界ブレンドカーブ形状
- `noise: Noise` - 境界ブレンドにノイズを付加
- `falloffRange: Vector2` - Box/Range用の境界ブレンド範囲

### Easing (`Packages/com.jbooth.microverse/Scripts/Easing.cs`)

```csharp
public enum BlendShape {
    Linear,      // リニア
    Smoothstep,  // スムーズステップ（S字）
    EaseIn,      // イーズイン
    EaseOut,     // イーズアウト
    EaseInOut    // イーズインアウト
}
public BlendShape blend = BlendShape.Linear;
```

シェーダーキーワード生成: `_FALLOFF` + `SMOOTHSTEP`/`EASEIN`/`EASEOUT`/`EASEINOUT`

## シェーダー処理フロー

`FalloffFilter.PrepareMaterial()` でSplineArea型の場合:
1. `_USEFALLOFFSPLINEAREA` キーワードを有効化
2. SDFテクスチャを `_FalloffTexture` に設定
3. `_FalloffAreaRange` に `splineAreaFalloff` を設定
4. `_FalloffAreaBoost` に `splineAreaFalloffBoost` を設定
5. Easingキーワードでカーブ形状を適用

## ガクガクになる原因

1. **splineAreaFalloff = 0**: 境界ブレンド距離がゼロ → 境界が完全に切れる
2. **Easing = Linear**: 直線的遷移 → 境界が角張る
3. **maxSDF < splineAreaFalloff**: SDFの計算範囲が足りない → 境界ブレンドが途中で途切れる
4. **sdfRes が低い**: SDF精度不足 → 境界がピクセル化する
