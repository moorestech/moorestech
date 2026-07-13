---
name: smooth-splinearea-boundary
description: |
  MicroVerseのSplineArea境界の地形・テクスチャ遷移を滑らかにする。
  バイオーム間のガクガクした段差や切れ目を自動的に解消する。

  Use When:
  - SplineAreaの境界で地形がガクガクしている
  - バイオーム間のテクスチャ遷移が急すぎる
  - SplineAreaの境界ブレンド（フォールオフ）を調整したい
  - MicroVerseのスプライン境界を滑らかにしたい
---

# SplineArea境界スムーズ化

> **表示名と内部名の対応**:
> - 表示名（UI）: **境界ブレンド**
> - 内部名（コード）: **Falloff**（`FalloffOverride`, `FalloffFilter`, `splineAreaFalloff` 等）
>
> クラス名・変数名はすべて内部名（Falloff）のまま。UI上の日本語表示のみ「境界ブレンド」を使用。

FalloffOverrideとSplineAreaコンポーネントのパラメータを調整し、バイオーム間の境界遷移を滑らかにする。

## 前提知識

- **SplineArea** (`SplineArea.cs`): 閉じたスプラインでエリアを定義。`maxSDF`と`sdfRes`を持つ。Smoothnessは持たない（SplinePathとは別クラス）。
- **FalloffOverride** (`FalloffOverride.cs`): バイオームコンテナに付与。内部の`FalloffFilter`が境界遷移を制御。UI表示名「境界ブレンドオーバーライド」。
- **FalloffFilter** (`FalloffFilter.cs`): `filterType=SplineArea`のとき`splineAreaFalloff`と`splineAreaFalloffBoost`で遷移幅を制御。UI表示名「境界ブレンドフィルター」。
- **Easing** (`Easing.cs`): `BlendShape`列挙型（Linear/Smoothstep/EaseIn/EaseOut/EaseInOut）で境界ブレンドカーブ形状を決定。

詳細は [references/architecture.md](references/architecture.md) を参照。

## ワークフロー

### 1. Before状態を撮影

```bash
uloop screenshot --window-name Scene
```

### 2. 現在の設定値を読み取り

```bash
uloop execute-dynamic-code --code '
using UnityEngine;
using JBooth.MicroVerseCore;
var results = new System.Collections.Generic.List<string>();
var fos = GameObject.FindObjectsByType<FalloffOverride>(FindObjectsSortMode.None);
foreach (var fo in fos) {
    var f = fo.filter;
    results.Add($"{fo.transform.parent.name}/{fo.name}:");
    results.Add($"  filterType: {f.filterType}");
    results.Add($"  easing: {f.easing.blend}");
    results.Add($"  splineAreaFalloff: {f.splineAreaFalloff}");
    results.Add($"  splineAreaFalloffBoost: {f.splineAreaFalloffBoost}");
}
var sas = GameObject.FindObjectsByType<SplineArea>(FindObjectsSortMode.None);
foreach (var sa in sas) {
    results.Add($"{sa.name}: sdfRes={sa.sdfRes}, maxSDF={sa.maxSDF}");
}
return string.Join("\n", results);
'
```

### 3. パラメータ調整

推奨値:

| パラメータ | デフォルト | 推奨 | 説明 |
|-----------|-----------|------|------|
| splineAreaFalloff | 0 | 20-50 | 境界ブレンド距離（フォールオフ距離） |
| splineAreaFalloffBoost | 0 | 5-15 | 境界ブレンド範囲の追加拡張 |
| easing.blend | Linear | EaseInOut | 境界ブレンドカーブ形状 |
| sdfRes | k512 | k1024 | SDF解像度 |
| maxSDF | 128 | falloffより大きく | SDF最大距離 |

**重要**: `maxSDF >= splineAreaFalloff + splineAreaFalloffBoost` にすること。不足すると境界ブレンドが途中で切れる。

全SplineAreaを一括調整:

```bash
uloop execute-dynamic-code --code '
using UnityEngine;
using UnityEditor;
using JBooth.MicroVerseCore;
var fos = GameObject.FindObjectsByType<FalloffOverride>(FindObjectsSortMode.None);
foreach (var fo in fos) {
    Undo.RecordObject(fo, "Smooth SplineArea Boundary");
    fo.filter.splineAreaFalloff = 30f;
    fo.filter.splineAreaFalloffBoost = 10f;
    fo.filter.easing.blend = Easing.BlendShape.EaseInOut;
    EditorUtility.SetDirty(fo);
}
var sas = GameObject.FindObjectsByType<SplineArea>(FindObjectsSortMode.None);
foreach (var sa in sas) {
    Undo.RecordObject(sa, "Smooth SplineArea SDF");
    sa.sdfRes = SplinePath.SDFRes.k1024;
    sa.maxSDF = 150f;
    EditorUtility.SetDirty(sa);
}
if (MicroVerse.instance != null) MicroVerse.instance.Invalidate();
return "Done";
'
```

### 4. After確認

MicroVerseの再描画を待ってから（約5秒）スクリーンショットを撮影:

```bash
uloop screenshot --window-name Scene
```

改善が不十分なら `splineAreaFalloff` を増やすか `Easing.BlendShape.Smoothstep` を試す。

## 特定のSplineAreaだけ調整する場合

```csharp
// パスは対象のバイオームコンテナに合わせて変更
var fo = GameObject.Find("MicroVerse/<SplineArea名>/<バイオーム名>")
    ?.GetComponent<FalloffOverride>();
if (fo != null) {
    Undo.RecordObject(fo, "Adjust falloff");
    fo.filter.splineAreaFalloff = 40f;
    EditorUtility.SetDirty(fo);
}
```
