# MapMaking 復元マニフェスト

元プロジェクト `/Users/katsumi/RiderProjects/MapMaking`（101GB）から、オリジナルのコード・設定・設定データのみを
moorestech リポジトリへ移行した際の記録（2026-07-13 実施）。有料アセット・生成出力・再取得可能な素材は除外した。
このディレクトリのファイル群を使えば、除外したアセットを再導入してプロジェクトを完全復元できる。

This is the restore manifest for the MapMaking Unity project migrated into the moorestech repository on 2026-07-13.
Paid assets, generated outputs, and re-downloadable materials were excluded; the tables here allow full restoration.

- **Unity バージョン**: 6000.3.8f1
- **シリアライズ**: シーン等はバイナリシリアライズだった（Force Text ではない）

## 復元手順

### 1. Pure Nature（BK）— 唯一コードベースから直接参照される有料アセット

`Assets/PersonalAssets/moorestech-client-private/` に非公開リポジトリを clone する（moorestech_client と同じ方式・gitignore 済み）:

```bash
cd Assets/PersonalAssets
git clone -b feature/add-microvers-terrain git@github.com:moorestech/moorestech-client-private.git
```

- **ピン**: `feature/add-microvers-terrain` @ `abb4749452434ad0dfc90361f5a3caba3f2865da`
  （このコミット時点で、本プロジェクトが参照する BK アセット全1300ファイルの存在を GUID 照合で検証済み）
- `pure-nature-references.tsv` — Biome プリセット等から BK への全243参照（参照元・参照先・GUID）
- `pure-nature-closure.txt` — 推移的依存を含む必要 BK ファイル全1300件
- `personal-assets-full-inventory.tsv` — 元プロジェクトの PersonalAssets 配下 全8,623ファイルの台帳（パス・GUID・サイズ）。
  参照されない BK 残余（Swamp/Mojave 等）は private リポジトリに未収録のものがあるが、この台帳と照合してストア再インポートで復元できる
- ストアから再インポートする場合: 「Pure Nature」「Pure Nature 2」(BK / Asset Store)。unitypackage は GUID を保持するため参照は自動復旧する

### 2. その他の除外アセット（コードベースからの参照ゼロ・エディタ作業用）

`excluded-assets-inventory.tsv` に全11,951ファイルのパス・GUID・サイズを記録。Asset Store から再インポートすれば GUID は一致する。

| アセット | 元の場所 | サイズ | 入手先 |
|---|---|---|---|
| MicroVerse (+ Vegetation/Ambiance/Splines/Objects/Demo) | `Packages/com.jbooth.microverse*` | 706MB | Asset Store (JBooth) |
| Rowlan Terrain Stamps | `Assets/Rowlan/Terrain/Stamps` | 3.4GB | Asset Store (Rowlan) |
| MicroVerse Presets 1〜5 (Rowlan/BK biome presets) | `Assets/MicroVerse-Presets*` | 707MB | Asset Store (Rowlan) |
| Gaia (Procedural Worlds) | `Assets/Procedural Worlds` | 2.3GB | Asset Store |
| Gaia 標準スタンプ | `Assets/Gaia User Data/Stamps` | 535MB | Gaia 再インストールで生成 |
| All In One - Heightmaps | `Assets/All In One - Heightmaps` | 767MB | Asset Store |
| COZY (stub) | `Packages/com.distantlands.cozy.core` | 180KB | Asset Store (Distant Lands) |
| Starter Assets | `Assets/StarterAssets` | 85MB | Asset Store (Unity 公式・無料) |
| TextMesh Pro Essentials | `Assets/TextMesh Pro` | 4.1MB | Window > TextMeshPro > Import TMP Essential Resources |
| フリー素材集 | `Assets/Dependencies` | 827MB | polyhaven.com / freestylized.com / texturecan.com / 3dtextures.me ほか（inventory 参照） |

### 3. 既知の欠損参照（承認済み）

`broken-references.tsv` — `Assets/Dependencies` 削除により Vein 系 prefab 5個が参照する
Stylised Nature のモデル/マテリアルと Noto フォント SDF（計10参照）が欠損する。
TMP の LiberationSans SDF は Essentials 再インポートで同一 GUID 復元される。

### 4. 捨てた生成出力（再生成可能）

`dropped-generated-outputs.tsv` — ExportMap.unity（87MB）・MapGeneratorTest.unity（65MB）・
Terrain_0_0.asset（76MB）・MapGenerator_Objects/Ores.prefab・TerrainData・テストゴールデン画像・ExportTest/。
いずれも MapGenerator の Presets（`Assets/MapGenerator/Presets/`）と Pipeline から再生成する。
テストゴールデンはテスト再実行で再生成される。

### 5. 元リポジトリのバックアップ

削除前に LFS 抜きの git bundle を作成（コミット履歴・コード・テキスト資産のみ。LFS 実体 35GB は含まない）:
`~/RiderProjects/MapMaking-backup.bundle`

## 移行時の検証結果（2026-07-13）

- スクリプト照合: 移行先の全参照 GUID を解決し、想定外の欠損ゼロ（欠損は本 README 記載の承認済み11件のみ）
- Unity 6000.3.8f1 実起動: コンパイルエラー0・コンソールエラー0
- BK 参照239アセットすべて `AssetDatabase.LoadMainAssetAtPath` でロード成功（239/239）
- TerrainMathTests: 15/15 パス
