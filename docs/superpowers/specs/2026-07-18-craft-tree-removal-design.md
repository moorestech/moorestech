# クラフトツリー完全削除 設計書

## 目的

今後使用しないクラフトツリーシステムを、クライアントUIだけでなくサーバー状態、通信、保存、依存関係、Unityアセット、現行ドキュメントまで含めて完全に削除する。無効化フラグや互換用の残骸は設けず、削除後のコードベースからクラフトツリーを実行・復元・表示できない状態にする。

## 非目標

- 代替となるレシピチェーン機能は実装しない。
- 旧クラフトツリーデータを別システムへ移行しない。
- 後方互換用のDTO、プロトコル、空実装、フォールバックを残さない。
- 過去の実装判断を記録した `docs/superpowers/` 配下の既存計画書・引き継ぎ資料は改変しない。

## 調査結果

クラフトツリーは約43個の製品ファイルに参照があり、専用コード・ディレクトリ・metaだけで38ファイル存在する。削除対象は次の6領域に分かれる。

1. サーバードメイン: `Game.CraftTree` アセンブリ、モデル、MessagePack DTO、JSON DTO、管理クラス。
2. サーバー統合: 取得・適用プロトコル、パケット登録、DI登録、保存組み立て、保存復元。
3. クライアント通信: 初期取得、初期ハンドシェイク集約結果、送信API、通信asmdef参照、デバッグ用レスポンス。
4. クライアントUI: エディタ、一覧、ターゲット、進捗更新、レシピ画面の作成導線、UI状態、DI登録。
5. Unityアセット: 専用Prefab 4個、共有Prefab 3個、作成・表示ボタン3個、関連するserialized参照。
6. 現行資料: uGUIスクリーン一覧と画像、Web UI移行TODO・監査資料の未実装項目。

## 削除設計

### Unityアセット

Unity固有YAMLはテキスト編集しない。`uloop execute-dynamic-code` からUnity Editor APIを呼び、`PrefabUtility.LoadPrefabContents` と `AssetDatabase.DeleteAsset` を使用してUnity自身にシリアライズさせる。

- `InventoryItems.prefab` から `CraftTree` ルート、2個の `RecipeTreeView` ボタン、旧 `show craft tree` 表示ボタンを削除する。
- `MainGameUI.prefab` から `CraftTreeTarget` のネストPrefabインスタンスを削除する。
- `GameSystem.prefab` の `MainGameStarter` から消えるserialized参照が残らないことを保存後に確認する。
- `CraftTreeEditorViewItem.prefab`、`CraftTreeListItem.prefab`、`CraftTreeTarget.prefab`、`CraftTreeTargetItem.prefab` を削除する。

アセット変更はクラフトツリーのMonoBehaviour型がまだ解決できる段階で先に実施し、共有Prefabから対象オブジェクトを安全に切り離してからコードを削除する。

### サーバー

`moorestech_server/Assets/Scripts/Game.CraftTree` をアセンブリごと削除する。`ApplyCraftTreeProtocol` と `GetCraftTreeProtocol` を削除し、`PacketResponseCreator` から両タグの登録を外す。`MoorestechServerDIContainerGenerator` から `CraftTreeManager` の登録を外し、全asmdefから `Game.CraftTree` 参照を削除する。

保存形式から `craftTreeInfo` を削除する。`WorldSaveAllInfoV1` のコンストラクタとプロパティ、`AssembleSaveJsonText` の依存と出力、`WorldLoaderFromJson` の依存と復元処理を一括で除去する。旧セーブに残る余剰JSONプロパティへの互換保証や移行コードは追加しない。

### クライアント

初期ハンドシェイクの並列取得から `GetCraftTree` を除き、`InitialHandshakeResponse` から `CraftTreeResponse` と関連タプル要素を削除する。`VanillaApiSendOnly.SendCraftTreeNode`、デバッグ用の空クラフトツリーレスポンス、`Client.Network` のasmdef依存も削除する。

`Client.Game/InGame/CraftTree` をディレクトリごと削除する。`RecipeViewerView` から作成ボタンとマネージャー参照を除き、`PlayerInventoryState` からマネージャー注入と非表示処理を除く。`MainGameStarter` からserialized fieldとVContainer登録を除き、`Client.Game` のasmdef依存を削除する。

### 現行ドキュメント

`docs/ugui-screenshot/screen-list.md` からクラフトツリー画面を除き、`25-craft-tree-planner.png` を削除する。`docs/webui/TODO.md`、`docs/webui/cef-webui-migration-todo.md`、`docs/webui/2026-07-07-parity-audit-verification-handoff.md` から移行対象・現存機能としての記載を除く。過去の計画書は履歴資料として保持する。

## 実装順序

1. Unity Editor経由で共有Prefabを整理し、専用Prefabを削除する。
2. サーバー保存・通信・DIの消費側を外し、`Game.CraftTree` を削除する。
3. クライアント通信・UI・DIの消費側を外し、専用UIコードを削除する。
4. asmdef参照と現行ドキュメントを整理する。
5. 残存参照、GUID、Missing Script、コンパイル、テスト、Errorログを検査する。

各実装タスクは `gpt-5.6-terra` の新規実装subagentへ順番に委譲し、同じファイルを並行編集しない。各タスクのコミット後に別subagentで仕様適合性とコード品質をレビューし、Critical・Important指摘を修正してから次へ進む。

## QAと完了条件

- 製品コードと現行資料に `CraftTree`、`craftTree`、`show craft tree`、`クラフトツリー`、`RecipeTreeView`、`va:getCraftTree`、`va:applyCraftTree` の意図しない参照がない。
- 削除対象スクリプト・PrefabのGUIDがPrefab、Scene、ScriptableObjectに残っていない。
- 影響した共有PrefabにMissing Script、Missing Prefab、壊れたobject referenceがない。
- 保存JSONに `craftTreeInfo` が出力されず、保存と復元が成功する。
- `PacketResponseCreator` と初期ハンドシェイクがクラフトツリー型なしで構築・実行できる。
- `uloop compile --project-path ./moorestech_client` が成功する。
- `AssembleSaveJsonTextTest` と `InitialHandshakeProtocolTest` を含む関連テストが成功する。
- `uloop get-logs --project-path ./moorestech_client --log-type Error` に今回の変更由来のエラーがない。
- 最終差分がプロジェクト規約を満たし、全変更がコミットされている。
