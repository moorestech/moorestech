# Skit 星空背景修復 設計

## 目的

開始Skit `Vanilla/Skit/skits/100_start_game` の宇宙シーンで、灰色になっている背景を `moorestech-client-private` の既存星空画像へ復旧する。

## 原因

`100_start_1_SpaceShip.prefab` の `SpaceSkybox` には `ObjectSkyboxCube Night` Materialが割り当てられているが、Materialの `_Tex` が参照するCubemap GUIDの実体が存在しない。Shaderは未指定時の灰色Cubemapを描画するため、背景が一様な灰色になる。

## 採用方式

`moorestech-client-private/EarthSimplePlanets/Skybox` にある6面画像を、`Hobione/Skybox/ObjectSkybox6side` Shaderを使うSkit専用Materialへ割り当てる。

- publicリポジトリにはSkit専用MaterialとPrefabの参照変更だけを保存する。
- privateリポジトリは既存の無視対象 `Assets/PersonalAssets/moorestech-client-private` へ配置し、publicリポジトリへ複製しない。
- Unity固有YAMLは直接編集せず、Unity Editor経由でMaterial作成とPrefab参照変更を行う。
- 欠損したCubemapの再生成は行わず、既存の6面画像を直接利用する。

## アセット構成

- 入力画像:
  - `EarthSimplePlanets/Skybox/frontImage.png`
  - `EarthSimplePlanets/Skybox/backImage.png`
  - `EarthSimplePlanets/Skybox/leftImage.png`
  - `EarthSimplePlanets/Skybox/rightImage.png`
  - `EarthSimplePlanets/Skybox/upImage.png`
  - `EarthSimplePlanets/Skybox/downImage.png`
- 新規Material: `Assets/Asset/Skit/Materials/SkitStarObjectSkybox.mat`
- 変更Prefab: `Assets/AddressableResources/Skit/Environment/100_start_1_SpaceShip.prefab`

## 設定内容

新規Materialには `Hobione/Skybox/ObjectSkybox6side` Shaderを設定し、6方向の各Texture propertyへprivate側の対応画像を割り当てる。内側から表示するためCullはFront、背景として前景を隠さない既存Shader既定の描画設定を維持する。

`SpaceSkybox` のMeshRendererは新規Materialを参照する。Skitコマンド、カメラ、環境ロード処理は変更しない。

## エラー時の扱い

private側の画像、対象Shader、対象Prefab、`SpaceSkybox` のいずれかが見つからない場合はアセット変更を行わず停止する。欠損状態のMaterialや推測した代替画像はコミットしない。

## 検証

1. Unity EditorでMaterialのShaderと6画像参照がすべて解決されていることを確認する。
2. `100_start_game` をPlayModeで再生し、「...レポート記録開始」の場面で星空背景が表示されることを確認する。
3. Game Viewのスクリーンショットを取得し、灰色背景が残っていないこと、惑星・宇宙船・UIの描画を壊していないことを確認する。
4. Unity ConsoleのErrorを確認する。
5. 最終差分をレビューし、検証画像をPRへ添付する。

## 非目標

- Skit背景システムやカメラ管理のリファクタリング
- 欠損した元Cubemap GUIDの復元
- private画像のpublicリポジトリへの追加
- 他シーンのSkybox変更
