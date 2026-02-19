# 制約事項・既知の問題・トラブルシューティング

## 実行方法の制約

### unity-test.sh（CliTestRunner）では実行不可

PlayModeテストは`EnterPlayMode`によるドメインリロードを含む。
`CliTestRunner`は`runSynchronously = true`で動作するため、ドメインリロード時に
`ResultCallbacks`インスタンスが破棄され、テスト結果が0件（passed: 0, failed: 0）として報告される。

**実行方法**:
- uLoop CLI: `uloop run-tests --port 56902 --filter-type regex --filter-value "Client\\.Tests\\.PlayModeTest\\.{ClassName}"`
- Unity Test Runnerウィンドウ: `Window > General > Test Runner` から手動実行

### worktree環境での制限

git worktree環境ではuLoopが使用できないため、PlayModeテストはworktree環境では実行不可。
メインのワーキングツリーで実行すること。

## EnterPlayModeの呼び出し位置制約

### `yield return new EnterPlayMode(...)` は `[UnityTest]` メソッド直下で呼ぶこと

`EnterPlayMode` はヘルパーメソッド内からyield returnしてもPlayModeに遷移しない（原因不明）。
必ず `[UnityTest]` 属性のついたIEnumeratorメソッドの直下で呼び出すこと。

```csharp
// 正しいパターン
// Correct pattern
[UnityTest]
public IEnumerator MyTest()
{
    EnterPlayModeUtil();  // 準備のみ（void）
    yield return new EnterPlayMode(expectDomainReload: true);  // [UnityTest]直下で呼ぶ
    // ...
}

// 間違ったパターン（PlayModeに遷移しない）
// Wrong pattern (will NOT enter PlayMode)
[UnityTest]
public IEnumerator MyTest()
{
    yield return SomeHelper();  // ヘルパー内でEnterPlayModeをyield returnしても動かない
}

IEnumerator SomeHelper()
{
    yield return new EnterPlayMode(expectDomainReload: true);  // ここからでは遷移しない
}
```

## ドメインリロード関連

### SessionStateの使用

ドメインリロードでstaticフィールドがリセットされるため、PlayMode遷移前後でフラグを保持するには`SessionState`を使用する。

```csharp
// 設定
SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);

// 取得（ドメインリロード後も保持される）
var disabled = SessionState.GetBool("DebugObjectsBootstrap_Disabled", false);
```

### DebugObjectsBootstrapの無効化

`DebugObjectsBootstrap`は`[RuntimeInitializeOnLoadMethod]`で自動起動する。
テスト中はSessionStateフラグで無効化する必要がある（`EnterPlayModeUtil()`内で自動設定される）。
テスト終了時に必ずフラグをクリアすること。

```csharp
// テスト終了時（ExitPlayModeの後）
SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
```

### LogAssert.ignoreFailingMessages

`EnterPlayMode`時にテストフレームワーク内部でエラーログが出力される場合がある。
これによるテスト失敗を防ぐため、EnterPlayMode直後に設定する。

```csharp
LogAssert.ignoreFailingMessages = true;
```

## AssetBundle関連

### 前回テストの残留AssetBundle

前回のテストで解放されなかったAssetBundleがあると、PlayMode遷移時に競合する。
`EnterPlayModeUtil()`内で`AssetBundle.UnloadAllAssetBundles(true)`が自動実行される。

## サーバーデータ

### PlayModeTestMod

PlayModeテスト用のマスターデータは `PlayModeTest/ServerData/mods/PlayModeTestMod/master/` に配置。
本番のVanillaModとは別のデータセットを使用する。

テストで新しいブロックやアイテムが必要な場合は、このディレクトリ内のJSONファイルを編集する。

### セーブデータ

`LoadMainGame()`はデフォルトでダミーのセーブファイルパスを生成するため、既存セーブは読み込まない。
特定のセーブデータでテストしたい場合は`saveFilePath`引数を指定する。

## タイムアウト

### LoadMainGameのタイムアウト

`LoadMainGame()`内部で`GameInitializerSceneLoader`の出現を最大60秒待機する。
60秒以内に表示されない場合はAssert.IsNotNullで失敗する。

テスト自体にもタイムアウトを設ける場合は`UniTask.WhenAny`パターンを使用する:

```csharp
var loadTask = LoadMainGame();
var timeOuter = UniTask.Delay(TimeSpan.FromSeconds(30));
var result = await UniTask.WhenAny(loadTask, timeOuter);
if (result == 1)
{
    Assert.Fail("LoadMainGame timed out.");
}
```
