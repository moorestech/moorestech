# 統一シャットダウンパイプライン設計

作成日: 2026-04-23
対象: moorestech_client / moorestech_server
発端: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs` の `GameShutdownEvent` 購読は WebUiHost 固有の責務ではなく、ゲーム全体の終了処理として一本化すべき、という指摘。クライアント・サーバーともに終了手順が散在しており、単一の起点を持たない。

---

## 1. 現状と問題点

### 1.1 クライアント側の終了経路（散在）

- `Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs`
  - `OnDestroy` / `OnApplicationQuit` / ボタン押下 の 3 経路から `Disconnect()` を呼ぶ
  - 順序は `SendOnly.Save()` → `Thread.Sleep(50)` → `VanillaApi.Disconnect()` → `GameShutdownEvent.FireGameShutdown()`
- `Client.WebUiHost/Boot/WebUiHost.cs`
  - `GameShutdownEvent.OnGameShutdown` を購読して Vite/Kestrel を停止
  - 追加で 3 種類の Unity Editor フック（`beforeAssemblyReload` / `EditorApplication.quitting` / `playModeStateChanged(ExitingPlayMode)`）
- `Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs`
  - `Application.quitting` に直接購読し Addressables 参照を解放
- `Client.Starter/MainGameStarter.cs`
  - `OnDestroy` で VContainer の `_resolver?.Dispose()`

### 1.2 サーバー側の終了経路

- `Server.Boot/ServerStarter.cs`
  - `OnDestroy` / `OnApplicationQuit` の両方から `_startServer.Dispose()`
- `Server.Boot/ServerInstanceManager.cs`
  - `Dispose()` が `_cancellationTokenSource.Cancel()` → `Thread.Abort()` → `GameUpdater.Dispose()`
  - 各操作を個別 `try`/`catch` で握り潰し

### 1.3 問題点

1. 起点が MonoBehaviour ライフサイクル + ボタン押下 + Editor フックに散っており、「ここを通れば必ず終了処理が実行される」という一本道がない
2. `WebUiHost` が `GameShutdownEvent` を直接購読しているため「Web UI 固有の話」と「ゲーム全体の終了処理」が混在している
3. 順序依存（Save → 接続切断 → サブシステム停止 → プロセス kill）が `BackToMainMenu.Disconnect` に直書きされており、参加者が増えるたびに同一ファイルを触る必要がある
4. Save の完了待ちが `Thread.Sleep(50)` という race condition smell に依存している
5. `Thread.Abort()` は .NET 推奨外であり、キャンセルトークンで停止する作りのスレッドに対しては不要
6. クライアントとサーバーで、構造的に同じ責務（アプリ終了と連動した確定的シャットダウン）を別々の流儀で実装している

---

## 2. 設計方針

### 2.1 前提

- ポーズメニューの「メインメニューに戻る」機能（`BackToMainMenu`）は削除する。セッション終了 = アプリ終了の 1 種類のみを扱う
- ただしロード画面中断ボタン・接続失敗時の自動遷移・切断ダイアログの戻るボタンなど、**エラー復帰 UI として MainMenu シーンへ戻る経路は残す**（`InitializeScenePipeline.backToMainMenuButton`、`NetworkDisconnectPresenter.goToMainMenuButton`、接続失敗時の `SceneManager.LoadScene(MainMenu)`）。これらは本 spec のスコープ外とし、将来別タスクで Coordinator 主導の終了 UI に差し替える
- クライアントが閉じる ⇒ クライアントが起動したローカルサーバープロセスも一緒に落ちる
- 後方互換性は考慮不要

### 2.2 2 層構造

#### Layer 1: `ShutdownCoordinator`（Unity 非依存・async のみ）

純粋な非同期オーケストレーション。`UnityEngine.Application` / `UnityEditor.*` を参照しない。クライアントとサーバーで namespace を分けて別コピーを持つ（フェーズ enum の値集合が異なるため共通基底は作らない）。

```csharp
public static class ShutdownCoordinator
{
    public static void Register(ShutdownPhase phase, string name, Func<UniTask> step);
    public static UniTask ShutdownAsync();
}
```

#### Layer 2: `ApplicationShutdownBridge`（Unity 側の終了シグナル → Coordinator）

Unity ライフサイクル（ランタイム / Editor）で発生する終了シグナルを捕捉し、`ShutdownCoordinator.ShutdownAsync()` を呼び出す。同期境界で `GetAwaiter().GetResult()` を使うのはこの Bridge 内のみ。

```csharp
internal static class ApplicationShutdownBridge
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallRuntimeHooks()
    {
        Application.quitting += TriggerBlocking;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InstallEditorHooks()
    {
        UnityEditor.EditorApplication.quitting += TriggerBlocking;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += TriggerBlocking;
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s == UnityEditor.PlayModeStateChange.ExitingPlayMode) TriggerBlocking();
        };
    }
#endif

    private static void TriggerBlocking()
    {
        var task = ShutdownCoordinator.ShutdownAsync().AsTask();
        Task.WhenAny(task, Task.Delay(Timeout)).GetAwaiter().GetResult();
    }
}
```

### 2.3 フェーズ定義

#### クライアント

```csharp
public enum ShutdownPhase
{
    BeforeDisconnect  = 0,    // Save ACK 待ち
    Disconnect        = 100,  // ソケットクローズ
    AfterDisconnect   = 200,  // サーバー不要なサブシステム停止（Web UI 等）
    DisposeSubsystems = 300,  // ローカルサーバー kill / Addressables 解放 / VContainer scope Dispose
}
```

#### サーバー

```csharp
public enum ShutdownPhase
{
    StopAcceptingConnections = 100,  // ServerListenAcceptor 停止
    StopUpdate               = 200,  // GameUpdater 停止 + スレッド終了待ち
    DisposeSubsystems        = 300,  // DI コンテナ / ネイティブリソース
}
```

終了時セーブはサーバー側では呼ばない。クライアントからの Save パケット任せとし、最悪失われるのは AutoSave 間隔分のみと割り切る。

### 2.4 Coordinator 挙動規則

1. **多重起動合流**: 初回呼び出し時にパイプラインを開始し `_shutdownTask` に保持。2 回目以降は同じ Task を返す
2. **実行順**: フェーズ番号昇順。同一フェーズ内は登録順に直列実行（並列にしない）
3. **例外ポリシー**: 各ステップを `try`/`catch` で包み、例外は `Debug.LogException` してから次ステップへ進む。全ステップ実行後でも Coordinator 自身は throw しない
4. **ステップ単体タイムアウト**: なし。全体タイムアウトは Bridge 側（5 秒）で持つ
5. **スレッドセーフ**: `Register` は `lock` で守る。パケット受信スレッド等から呼ばれても安全
6. **ログ**: 各ステップ開始/完了を `Debug.Log` で出す
7. **開始後の `Register`**: 無視して warning ログ。パイプライン実行中に後から来た登録は拾わない

### 2.5 起動直後の終了シグナル

`BeforeSceneLoad` のフックで `Application.quitting` を購読する時点では、参加者はまだ `Register` していない可能性がある。登録 0 件の Coordinator は即完了する挙動で割り切る（実害なし）。

---

## 3. 参加者と登録内容

### 3.1 クライアント

| 登録元 | Phase | 内容 |
|---|---|---|
| `VanillaApi.Initialize` | `BeforeDisconnect` | `await Response.SaveAsync()` |
| `VanillaApi.Initialize` | `Disconnect` | `_serverCommunicator.Close()` |
| `VanillaApi.Initialize` | `DisposeSubsystems` | `_localServerProcess?.Kill()` |
| `WebUiHost.StartAsync`（初回起動時 1 回） | `AfterDisconnect` | `await StopAsync()` |
| `DebugObjectsBootstrap` の `InitializeOnLoadMethod` | `DisposeSubsystems` | `_debugObjectsAsset?.Dispose()` |
| `MainGameStarter.StartGame` | `DisposeSubsystems` | `_resolver?.Dispose()` |

### 3.2 サーバー

| 登録元 | Phase | 内容 |
|---|---|---|
| `ServerInstanceManager.Start` | `StopAcceptingConnections` | `_cancellationTokenSource.Cancel()` |
| `ServerInstanceManager.Start` | `StopUpdate` | `_connectionUpdateThread.Join(timeout)` / `_gameUpdateThread.Join(timeout)` |
| `ServerInstanceManager.Start` | `DisposeSubsystems` | `GameUpdater.Dispose()` |

`Thread.Abort()` は廃止。両スレッドは `CancellationToken` を見ているため、Cancel 後に Join で自然終了を待つ。timeout 到達時は warning ログのみ（強制終了できない代わりに安全を選ぶ）。

---

## 4. Save ACK プロトコル追加

`Thread.Sleep(50)` を廃止し、Save 完了を明示的に待つ。

- サーバー側に Save 完了レスポンスプロトコルを新設（`creating-server-protocol` スキルに従う）
- `VanillaApiWithResponse.SaveAsync()` を追加：リクエストを送信し、サーバーが保存完了を返したら complete する `UniTask`
- `SaveButton` は `SendOnly.Save` → `Response.SaveAsync` に切り替え、押下時も完了を待つ
- 既存 `VanillaApiSendOnly.Save` は `BackToMainMenu` 削除と同時に削除

---

## 5. 削除対象

- `Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs` 全削除
  - ポーズメニュー Prefab/シーンからの参照剥がしはユーザー作業（PR 本文に明記）
- `Client.Game/Common/GameShutdownEvent.cs` 全削除
- `WebUiHost` 内の以下
  - `_shutdownSubscription`
  - `_stopTask`
  - `CleanupAllSync` / `RegisterDomainReloadHook` / `OnPlayModeStateChanged`
  - `GameShutdownEvent.OnGameShutdown` 購読
- `ServerStarter.OnDestroy` / `OnApplicationQuit` / `FinishServer` / `_startServer` フィールド
- `ServerInstanceManager.Dispose` / `IDisposable` 実装
- `DebugObjectsBootstrap` の `Application.quitting` 直接購読
- `VanillaApiSendOnly.Save`（Response.SaveAsync への置換完了後）

---

## 6. 実装順序

1. **Save ACK プロトコル追加**（独立して入る）
2. **`ShutdownCoordinator` + `ApplicationShutdownBridge` 本体**（クライアント・サーバー、それぞれ独立。既存の終了処理は手付かず）
3. **クライアント参加者の移行**
   - `VanillaApi` 3 ステップ登録
   - `WebUiHost` を Coordinator 登録に差し替え + Editor フック削除
   - `DebugObjectsBootstrap` 差し替え
   - `MainGameStarter._resolver?.Dispose()` 差し替え
4. **サーバー参加者の移行**
   - `ServerInstanceManager.Start` が 3 ステップ登録
   - `Thread.Abort` → `Join(timeout)`
   - `ServerStarter` / `ServerInstanceManager` の終了系削除
5. **旧仕組みの除去**
   - `BackToMainMenu.cs` / `GameShutdownEvent.cs` 削除
   - `WebUiHost` 内の残存ガード削除
   - `SaveButton` 切り替え + `SendOnly.Save` 削除

中間状態を main に出さないため、段階 3〜5 は単一 PR でまとめる。段階 1〜2 は先行 PR にしても良い。

---

## 7. テスト方針

### 7.1 Coordinator 単体テスト（Edit Mode）

- 登録順と実行順がフェーズ → 登録順であること
- 多重 `ShutdownAsync` 呼び出しが同じ Task を返すこと
- ステップ内例外で次ステップに進むこと
- クライアント版・サーバー版の両方で対称テスト

### 7.2 Bridge / 参加者統合（手動検証）

`Application.quitting` を人工発火できないため、手動チェックリストで代替：

- Editor Play Mode Stop → ログにフェーズ順実行
- Editor 終了 → Web UI 停止、ポート解放
- 再生中に `.cs` 編集でドメインリロード → Kestrel/Vite 停止
- ビルド版起動 → ウィンドウ閉じる → ローカルサーバープロセス終了確認

### 7.3 回帰テスト

- Save ACK 追加に伴い、既存のセーブ関連テストが通ること
- 既存 PlayMode テスト（起動系）が破壊的変更後も通ること

---

## 8. リスクと緩和

| リスク | 緩和策 |
|---|---|
| Save ACK サーバー側実装漏れでクライアントが timeout 5 秒待ち続ける | Coordinator の各ステップ診断ログで、Save ステップが 5 秒フル使う状態を早期検知 |
| `Thread.Join(timeout)` でスレッドが抜けない場合のリソースリーク | timeout 到達は warning ログのみ。Abort 廃止で「強制終了できない代わりに安全」を選ぶ |
| `BackToMainMenu` 削除で Prefab/シーンにダングリング参照が残る | PR 本文で「シーン/Prefab 手編集要」と明記し、参照箇所を検索結果で提示 |
| Editor フック移行中の二重購読（Coordinator と WebUiHost 旧コード両方が発火） | 実装順序 3〜5 を 1 PR で通し、中間状態を main に出さない |
| Addressables 解放が Web UI 停止より前に走ると参照中アセットが巻き込まれる | フェーズ分離で担保（`AfterDisconnect` → `DisposeSubsystems`） |
| `BeforeSceneLoad` フック時点で参加者が 0 件の場合、即完了する Coordinator が空回り | 登録 0 件は no-op 完了として割り切る（実害なし） |

---

## 9. 未決事項

なし（§1〜§4 で全て合意済み）。

## 10. フォローアップ（別タスク）

- `NetworkDisconnectPresenter.goToMainMenuButton`・`InitializeScenePipeline.backToMainMenuButton`・接続失敗時の `SceneManager.LoadScene(MainMenu)` を整理し、エラー復帰フローを ShutdownCoordinator 主導の UI に統合する
- `Steps_RunInPhaseThenRegistrationOrder` テストを要素数 17 以上に拡張し、stable sort 契約を明示的に回帰テスト化する
- `Task.Delay(Timeout)` を `CancellationToken` でキャンセル可能にして完了側パスでリソースを即解放する
- サーバー側に `Register_AfterShutdown_IsIgnored` テストを追加してクライアントと対称化する
