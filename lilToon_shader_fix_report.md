# lilToon シェーダーエラー修正レポート

## 症状

Scene Viewでキャラクターモデルがマゼンタ（ピンク）で表示される。
Consoleに以下のシェーダーコンパイルエラーが出力される。

```
Shader error in 'Hidden/ltspass_opaque': redefinition of 'LIGHTMAP_ON'
at Packages/jp.lilxyzw.liltoon/Shader/ltspass_opaque.shader
```

---

## 根本原因

2つの独立した問題が重なっていた。

### 原因1: GUID解決先の競合（空ディレクトリ問題）

lilToonの実ファイルは `Assets/Dependencies/lilToon/` に存在するが、`Packages/jp.lilxyzw.liltoon/` に**空のディレクトリ構造**が残存していた。

```
Packages/jp.lilxyzw.liltoon/
├── BaseShaderResources/   (空)
├── CustomShaderResources/  (空サブディレクトリのみ)
├── Editor/                (空)
├── Shader/                (空)
└── ...
```

Unityのアセットデータベースは、この空ディレクトリに対してもGUIDを割り当てる。lilToonの内部コード（`lilDirectoryManager.cs`、`lilShaderContainerImporter.cs`）はハードコードされたGUIDでファイルパスを解決するが、これらのGUIDが空の`Packages/`側を指していたため、シェーダー再生成に必要なテンプレートファイル（`.lilinternal`、`.lilblock`）が見つからなかった。

**結果**: シェーダーリフレッシュ（`Assets/lilToon/[Shader] Refresh shaders`）を実行しても、テンプレートが読めないため`.shader`ファイルが更新されず、古いBRP（Built-in Render Pipeline）用シェーダーが残り続けた。

#### GUID解決の具体例

| パス解決メソッド | ハードコードGUID | 解決先（修正前） | 解決先（修正後） |
|---|---|---|---|
| `GetBaseShaderFolderPath()` | `d465bb256af2...` | `Packages/jp.lilxyzw.liltoon/BaseShaderResources` (空) | `Assets/Dependencies/lilToon/BaseShaderResources` |
| `GetShaderFolderPath()` | `ac0a8f602b5e...` | `Packages/jp.lilxyzw.liltoon/Shader` (空) | `Assets/Dependencies/lilToon/Shader` |
| `GetEditorFolderPath()` | `3e73d675b9c1...` | `Packages/jp.lilxyzw.liltoon/Editor` (空) | `Assets/Dependencies/lilToon/Editor` |
| `GetCustomShaderResourcesFolderPath()` | `1acd4e79a7d2...` | `Packages/jp.lilxyzw.liltoon/CustomShaderResources` (空) | `Assets/Dependencies/lilToon/CustomShaderResources` |

### 原因2: `#pragma skip_variants` と `#pragma multi_compile` の競合（Metal固有）

lilToonのシェーダー生成システムは、シェーダーバリアント数を削減するために`HLSLINCLUDE`ブロック（全パス共通）に`#pragma skip_variants`を配置する。一方、ForwardパスではURP用の`#pragma multi_compile`が同じキーワードを宣言する。

```hlsl
// HLSLINCLUDE（全パス共通）
#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON ...
#pragma skip_variants _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
#pragma skip_variants _ADDITIONAL_LIGHT_SHADOWS
#pragma skip_variants _SCREEN_SPACE_OCCLUSION

// Forwardパス
#pragma multi_compile _ LIGHTMAP_ON          // ← 競合！
#pragma multi_compile _ _DBUFFER_MRT1 ...    // ← 競合！
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS  // ← 競合！
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION    // ← 競合！
```

macOSのMetalシェーダーコンパイラは、同一キーワードに対する`skip_variants`と`multi_compile`の共存を「再定義エラー」として扱う。他のプラットフォーム（Windows/D3D11等）では問題にならないため、これはMetal固有の問題。

---

## 修正内容

### 修正1: 空ディレクトリの削除

```bash
rm -rf moorestech_client/Packages/jp.lilxyzw.liltoon/
```

空のディレクトリ構造を完全に削除し、GUIDの競合元を排除した。

### 修正2: `lilDirectoryManager.cs` のGUID更新

`Assets/Dependencies/lilToon/` の`.meta`ファイルに記載されているGUIDに書き換えた。

**ファイル**: `Assets/Dependencies/lilToon/Editor/lilDirectoryManager.cs`

| メソッド | 旧GUID | 新GUID | 備考 |
|---|---|---|---|
| `GetPackageJsonPath()` | `397d2fa9e93f...` | `3d0fceb0fac2...` | |
| `GetBaseShaderFolderPath()` | `d465bb256af2...` | `10192b226f79...` | |
| `GetEditorFolderPath()` | `3e73d675b9c1...` | `1c21482cf9c2...` | |
| `GetPresetsFolderPath()` | `35817d21af2f...` | `a41c530ac971...` | |
| `GetEditorPath()` | `aefa51cbc37d...` | `43457116803c...` | パスも `Editor/lilInspector/lilInspector.cs` に変更 |
| `GetShaderFolderPath()` | `ac0a8f602b5e...` | `82bf9e58018a...` | |
| `GetShaderCommonPath()` | `5520e76642295...` | `f06a6e421bf7...` | |

以下は対応する`.meta`ファイルが存在しないため、親フォルダからのパス構築に変更した。

```csharp
// 変更前: GUIDで解決（解決先が存在しない）
public static string GetShaderPipelinePath() => GUIDToPath("32299664512e...");
public static string GetCurrentRPPath()      => GUIDToPath("142b3aeca721...");

// 変更後: 親フォルダからパス構築
public static string GetShaderPipelinePath() => GetShaderFolderPath() + "/Includes/lil_pipeline.hlsl";
public static string GetCurrentRPPath()      => GetEditorFolderPath() + "/CurrentRP.txt";
```

GUIリソース（`.guiskin` → `.png`）もAssets側のGUIDに更新した。

### 修正3: `lilShaderContainerImporter.cs` のGUID更新

**ファイル**: `Assets/Dependencies/lilToon/Editor/lilShaderContainerImporter.cs`

```csharp
// 変更前
private const string customShaderResourcesFolderGUID = "1acd4e79a7d2c6c44aa8b97a1e33f20b";

// 変更後
private const string customShaderResourcesFolderGUID = "08e68c6398c0b498a9faeeb881c3ca20";
```

### 修正4: `skip_variants` の競合解消

**ファイル**: `Assets/Dependencies/lilToon/Editor/lilShaderContainerImporter.cs`

BRP以外のレンダーパイプラインでは、Forwardパスの`multi_compile`と競合する`skip_variants`を無効化した。

```csharp
// 変更前
private static string GetSkipVariantsLightmaps()
{
    return "#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON ...";
}

// 変更後
private static string GetSkipVariantsLightmaps()
{
    if(version.RP != lilRenderPipeline.BRP) return "";
    return "#pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON ...";
}
```

同様の修正を以下のメソッドにも適用した。

| メソッド | 対象キーワード |
|---|---|
| `GetSkipVariantsLightmaps()` | `LIGHTMAP_ON`, `DYNAMICLIGHTMAP_ON`, `LIGHTMAP_SHADOW_MIXING`, `SHADOWS_SHADOWMASK`, `DIRLIGHTMAP_COMBINED`, `_MIXED_LIGHTING_SUBTRACTIVE` |
| `GetSkipVariantsDecals()` | `DECALS_OFF`, `DECALS_3RT`, `DECALS_4RT`, `DECAL_SURFACE_GRADIENT`, `_DBUFFER_MRT1`, `_DBUFFER_MRT2`, `_DBUFFER_MRT3` |
| `GetSkipVariantsAddLightShadows()` | `_ADDITIONAL_LIGHT_SHADOWS` |
| `GetSkipVariantsAO()` | `_SCREEN_SPACE_OCCLUSION` |
| `GetSkipVariantsReflections()` | `_REFLECTION_PROBE_BLENDING`, `_REFLECTION_PROBE_BOX_PROJECTION` |

### 修正5: マテリアルのシェーダー再割当

シェーダー参照が失われた9つのマテリアルに `lilToon` シェーダーを再割当した。これらのマテリアルは削除された`Packages/jp.lilxyzw.liltoon/`内のシェーダーGUIDを参照していたため、`Hidden/InternalErrorShader`にフォールバックしていた。

---

## 修正後のシェーダー再生成フロー

修正後、`Assets/lilToon/[Shader] Refresh shaders` メニューが正常に動作するようになった。

```
1. lilDirectoryManager がAssets/Dependencies/lilToon/の正しいパスを返す
2. BaseShaderResources/ から .lilinternal テンプレートを読み込む
3. lilRenderPipelineReader.GetRP() で URP を検出
4. CustomShaderResources/URP/ から URP用 .lilblock テンプレートを読み込む
5. UnpackContainer() が URP用 SubShader を生成（lil_pipeline_urp.hlsl を include）
6. skip_variants は BRP以外では空文字を返すため、multi_compile との競合なし
7. 生成された .shader ファイルが Shader/ フォルダに書き出される
```

---

## 影響範囲

- **修正ファイル**: 2ファイル（`lilDirectoryManager.cs`、`lilShaderContainerImporter.cs`）
- **削除ディレクトリ**: `Packages/jp.lilxyzw.liltoon/`（空ディレクトリ構造のみ）
- **シェーダーバリアント数**: URP/HDRP/LWRPでは`skip_variants`を無効化したため、わずかにバリアント数が増加する可能性がある。実用上の影響はほぼない。
- **BRP互換性**: BRPでは従来通り`skip_variants`が有効なため、既存動作に影響なし。

---

## 再発防止

`Packages/jp.lilxyzw.liltoon/` のような空ディレクトリが再作成されると、同じ問題が再発する可能性がある。lilToonのアップデート時は、旧パスのディレクトリが残らないよう注意が必要。
