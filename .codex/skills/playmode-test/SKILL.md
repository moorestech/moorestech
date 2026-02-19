---
name: playmode-test
description: >
  clientのPlayModeテスト（EditModeから実行しPlayModeに遷移する統合テスト）を作成するスキル。
  Use when:
  (1) moorestech_clientに新しいPlayModeテストを追加する時
  (2) 「PlayModeテストを書いて」「クライアントの統合テストを作成して」と依頼された時
  (3) ゲーム起動後のUI・エンティティ・ビジュアル検証テストを作成する時
---

# PlayMode Test Creator

moorestech_clientのPlayModeテスト作成ガイド。EditModeテストとして実行し、途中でPlayModeに遷移する特殊な統合テスト。

## 概要

PlayModeテストは実際にゲームを起動し、クライアント側の動作（UI表示、エンティティ描画、物理演算等）を検証する。
サーバーもクライアントプロセス内で同時起動するため、`ServerContext`と`ClientContext`の両方にアクセス可能。

# 重要な前提
あくまでUnity側からの実行方法はEditModeTestである点に注意。EditModeTest内部でUnityを再生して実行するテストを、ここではPlayModeTestと読んでいる

## ワークフロー

### 1. テストクラスを作成

配置先: `moorestech_client/Assets/Scripts/Client.Tests/PlayModeTest/`

テストメソッドの基本構造（詳細テンプレートは [references/template.md](references/template.md) を参照）:

1. `yield return EnterPlayModeUtil();` - PlayMode遷移
2. `LogAssert.ignoreFailingMessages = true;` - フレームワーク内部エラー抑制
3. `yield return TestBody().ToCoroutine();` - async処理をIEnumeratorに変換して実行
4. `yield return new ExitPlayMode();` - PlayMode終了
5. `SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);` - フラグクリア

テンプレート種別:
- **基本テンプレート** - ゲーム起動して検証
- **UIテストテンプレート** - UI要素の表示・状態検証
- **エンティティ/物理テストテンプレート** - 時間経過に伴う挙動検証
- **タイムアウト付きテンプレート** - 起動タイムアウト設定

### 2. 必須ルール

- **`[UnityTest]` + `IEnumerator`** を使用（`[Test]`ではない）
- **`EnterPlayModeUtil()`** を最初にyield return（SessionState設定とAssetBundleクリーンアップを含む）
- **`LogAssert.ignoreFailingMessages = true`** をEnterPlayMode直後に設定
- **`ExitPlayMode`** をテスト終了時にyield return
- **`SessionState.SetBool("DebugObjectsBootstrap_Disabled", false)`** でフラグクリア
- **async処理は`.ToCoroutine()`** でIEnumeratorに変換
- **コメントは日英2行セット** で記述
- **try-catch禁止** - 条件分岐で対応
- **デフォルト引数禁止** - 呼び出し側を変更
- **複雑なテストでは `#region Internal` + ローカル関数** を使用
- **`#endregion` の下にコードを書かない**

### 3. PlayModeTestUtil ヘルパーAPI

詳細は [references/helper-api.md](references/helper-api.md) を参照。

| メソッド | 用途 |
|---------|------|
| `EnterPlayModeUtil()` | PlayMode遷移（SessionState設定 + AssetBundleクリーンアップ含む） |
| `LoadMainGame(serverDirectory, saveFilePath)` | ゲーム起動してメインシーンロード |
| `GiveItem(itemName, count)` | プレイヤーにアイテム付与 |
| `PlaceBlock(blockName, position, direction)` | サーバー側にブロック設置 |
| `RemoveBlock(position)` | サーバー側のブロック除去 |
| `InsertItemToBlock(block, itemId, count)` | ブロックインベントリにアイテム挿入 |

### 4. テスト実行

PlayModeテストはドメインリロードが発生するため、**uLoop CLI経由でのみ実行可能**。
`unity-test.sh`（CliTestRunner）は`runSynchronously = true`で動作するため対応不可。

```
# uLoop CLI（推奨・唯一の方法）
uloop run-tests --port 56902 --filter-type regex --filter-value "Client\\.Tests\\.PlayModeTest\\.{ClassName}"
```

**重要**: テスト結果はドメインリロード後に報告されるため、通常のテストより長い待ち時間が必要。

### 5. 制約事項

詳細は [references/constraints.md](references/constraints.md) を参照。

## リソース

- [references/template.md](references/template.md) - 基本テンプレートと応用パターン
- [references/helper-api.md](references/helper-api.md) - PlayModeTestUtilの全APIリファレンス
- [references/constraints.md](references/constraints.md) - 制約事項、既知の問題、トラブルシューティング
