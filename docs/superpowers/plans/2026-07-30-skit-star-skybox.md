---
spec: ../specs/2026-07-30-skit-star-skybox-design.md
status: approved
approved_at: 2026-07-30
---

# Skit 星空背景修復 実装計画

## ゴール

開始Skit `Vanilla/Skit/skits/100_start_game` の宇宙背景を、`moorestech-client-private` に存在する6面星空画像で表示し、Game ViewのスクリーンショットとUnityログで修復を確認してPRを作成する。

## 変更範囲

- privateアセットを無視対象の `Assets/PersonalAssets/moorestech-client-private` へ配置する。
- Unity Editor経由で `Assets/Asset/Skit/SkitStarObjectSkybox.mat` を作成する。
- Unity Editor経由で `100_start_1_SpaceShip.prefab` の `SpaceSkybox` に新Materialを割り当てる。
- Skitの処理コード、カメラ、他のPrefabは変更しない。

## Task 1: privateリポジトリを配置する

1. 既存ローカルcloneのremote、commit、作業ツリー状態を確認する。
2. `moorestech_client/Assets/PersonalAssets/moorestech-client-private` へローカルcloneする。
3. 6面画像と各 `.meta` が揃い、public側のGit差分に現れないことを確認する。

## Task 2: Unity実行環境を準備する

1. 現worktreeの `Library` 有無を確認する。
2. `Library` がない場合は、稼働中の別Unityを終了させず、メインworktreeの `Library` を現worktreeへコピーする。
3. uloop CLIを公式の配布元から利用可能にし、現worktreeのUnity Editorを起動する。
4. Unityの初期importとdomain reloadが完了するまで待つ。

## Task 3: MaterialとPrefabをEditor経由で変更する

`uloop execute-dynamic-code` で以下を一度に実行する。

1. `Shader.Find("Hobione/Skybox/ObjectSkybox6side")` と6面Textureを事前検証する。
2. `SkitStarObjectSkybox` Materialを作成し、`_FrontTex`、`_BackTex`、`_LeftTex`、`_RightTex`、`_UpTex`、`_DownTex` を対応画像へ設定する。
3. `_Cull = 1`、`_ZWrite = 1` を設定する。
4. `PrefabUtility.LoadPrefabContents` で対象Prefabを開き、`SpaceSkybox` の `MeshRenderer.sharedMaterial` を置換する。
5. PrefabとMaterialを保存し、AssetDatabaseを更新する。
6. Unity EditorからMaterialのShader、全Texture参照、PrefabのMaterial参照を再読込して検証する。

失敗条件はShader、Texture、Prefab、`SpaceSkybox`、MeshRendererのいずれかが見つからない場合とし、事前検証が通るまでアセットを作成しない。

## Task 4: PlayModeでバグ狩りを行う

1. `Assets/Scenes/Other/SkitTest.unity` を開く。
2. PlayModeを開始し、`100_start_game` の最初の台詞が描画されるまで待つ。
3. Game Viewのrendering screenshotを取得する。
4. 次の観点を目視確認する。
   - 背景が一様な灰色でなく星空になっている。
   - 6面の向きに明白な破綻、黒面、ピンクMaterialがない。
   - 惑星、宇宙船、樹木、台詞UIが従来どおり表示される。
   - 星空が前景を隠していない。
5. Unity ConsoleのErrorを取得し、新規Errorがないことを確認する。
6. PlayModeを停止する。

問題を見つけた場合は原因へ戻り、Editor経由で修正して同じ検証を繰り返す。

## Task 5: 差分を検証してコミットする

1. Unityが生成したMaterial、`.meta`、Prefab差分だけが意図した変更であることを確認する。
2. privateアセット、Library、Temp、LogsがGit差分へ混入していないことを確認する。
3. バイナリ画像やPrefabの不要な再シリアライズがないことを確認する。
4. 実装と検証画像をコミットする。

## Task 6: PR前レビューとPR作成

1. `moores-code-review` に従って決定論チェックと独立レビューを行う。
2. 指摘を修正し、必要なUnity検証を再実行する。
3. branchをpushする。
4. `pr-create` に従って原因、修正、QA結果、スクリーンショットを含むPRを作成する。

## アーキテクチャ確認

- レイヤー追加やassembly依存の変更はない。
- publicのMaterial/Prefabが、既存方針どおりgitignoredなprivateアセットを参照する。
- 実行時分岐やフォールバックは追加せず、欠損はUnity参照として明示的に検出する。
- Skit専用Materialに閉じるため、共通Shaderへドメイン固有設定を持ち込まない。

## 完了条件

- 星空がGame Viewで表示される。
- Unity Consoleに修正起因のErrorがない。
- 意図したUnityアセット差分がコミット済みである。
- PRが作成され、検証スクリーンショットを確認できる。
