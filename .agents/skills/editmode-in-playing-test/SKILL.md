---
name: editmode-in-playing-test
description: >
  clientのEditModeInPlayingTest（EditModeテストとして実行し、途中でPlayModeに遷移する統合テスト）を作成するスキル。
  Use when:
  (1) moorestech_clientのEditModeInPlayingTestに新しいテストを追加する時
  (2) 「PlayModeテストを書いて」「クライアントの統合テストを作成して」と依頼された時
  (3) ゲーム起動後のUI・エンティティ・ビジュアル検証テストを作成する時
---

# EditModeInPlayingTest Creator

moorestech_clientの統合テスト作成ガイド。EditModeテストとして実行し、途中で`EnterPlayMode`によりPlayModeに遷移する特殊なテスト。

## 概要

実際にゲームを起動し、クライアント側の動作（UI表示、エンティティ描画、物理演算等）を検証する。
サーバーもクライアントプロセス内で同時起動するため、`ServerContext`と`ClientContext`の両方にアクセス可能。

# 重要な前提
あくまでUnity側からの実行方法はEditModeTestである点に注意。EditModeTest内部でUnityを再生して実行する。

## ワークフロー

### 1. テストクラスを作成

配置先: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/`
namespace: `Client.Tests.EditModeInPlayingTest`

テストメソッドの基本構造（詳細テンプレートは [references/template.md](references/template.md) を参照）:

1. `EnterPlayModeUtil();` - 通常メソッド呼び出し（SessionState設定 + AssetBundleクリーンアップ）
2. `yield return new EnterPlayMode(expectDomainReload: true);` - **必ず[UnityTest]メソッド直下で呼ぶ**（そうしないとPlayModeに入らない）
3. `LogAssert.ignoreFailingMessages = true;` - フレームワーク内部エラー抑制
4. `yield return TestBody().ToCoroutine();` - async処理をIEnumeratorに変換して実行
5. `yield return new ExitPlayMode();` - PlayMode終了
6. `SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);` - フラグクリア

テンプレート種別:
- **基本テンプレート** - ゲーム起動して検証
- **UIテストテンプレート** - UI要素の表示・状態検証
- **エンティティ/物理テストテンプレート** - 時間経過に伴う挙動検証
- **タイムアウト付きテンプレート** - 起動タイムアウト設定

### 2. 必須ルール

- **`[UnityTest]` + `IEnumerator`** を使用（`[Test]`ではない）
- **`EnterPlayModeUtil()`** を最初に呼ぶ（voidメソッド。yield returnしない）
- **`yield return new EnterPlayMode(expectDomainReload: true)`** を[UnityTest]メソッド直下で呼ぶ
- **`LogAssert.ignoreFailingMessages = true`** をEnterPlayMode直後に設定
- **`ExitPlayMode`** をテスト終了時にyield return
- **`SessionState.SetBool("DebugObjectsBootstrap_Disabled", false)`** でフラグクリア
- **async処理は`.ToCoroutine()`** でIEnumeratorに変換
- **OS入力注入を使うテストには`[Category("IgnoreCI")]`** を付与
- テストヘルパーの既存APIに限りデフォルト引数を許容する（AGENTS.mdのデフォルト引数禁止の例外）

### 3. EditModeInPlayingTestUtil ヘルパーAPI

`Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil`（`using static`推奨）。
詳細は [references/helper-api.md](references/helper-api.md) を参照。

| メソッド | 用途 |
|---------|------|
| `EnterPlayModeUtil()` | PlayMode遷移準備（SessionState設定 + AssetBundleクリーンアップ）。void |
| `LoadMainGame(serverDirectory, saveFilePath)` | ゲーム起動してメインシーンロード |
| `GiveItem(itemName, count)` | プレイヤーにアイテム付与 |
| `PlaceBlock(blockName, position, direction)` | サーバー側にブロック設置 |
| `WaitBlockGameObjectSpawn(position)` | クライアント側BlockGameObjectのスポーン待機 |
| `RemoveBlock(position)` | サーバー側のブロック除去 |
| `InsertItemToBlock(block, itemId, count)` | ブロックインベントリにアイテム挿入 |

### 4. テスト実行

ドメインリロードが発生するため、**uloop CLI経由でのみ実行可能**。

```bash
uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.EditModeInPlayingTest\.{ClassName}"
```

ドメインリロードでuloopが一度切断される。結果が取れない場合の読み方は [references/constraints.md](references/constraints.md)。

### 5. 制約事項

詳細は [references/constraints.md](references/constraints.md) を参照。

## リソース

- [references/template.md](references/template.md) - 基本テンプレートと応用パターン
- [references/helper-api.md](references/helper-api.md) - EditModeInPlayingTestUtilの全APIリファレンス
- [references/constraints.md](references/constraints.md) - 制約事項、既知の問題、トラブルシューティング
