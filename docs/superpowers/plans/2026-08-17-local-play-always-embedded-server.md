# Local Play Always Boots Embedded Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ローカルプレイの「11564へ接続試行→失敗したら内蔵サーバー起動」というprobe→フォールバック構造を反転し、ローカルは必ず内蔵サーバー(port 0)を起動・外部接続は明示指定のみ・失敗時はフォールバックせずエラー復帰にする。

**Architecture:** `InitializeProprieties` をファクトリ2本（`CreateLocalServer` / `CreateRemoteConnection`）に絞りモードを内部保持。`ServerConnectionInitializer` はモードで分岐し、SocketException捕捉によるフォールバックtry-catchを削除。あわせて11564残骸（`ServerConst.LocalServerPort`・`MainGameStarter`死にフィールド・常時nullの`LocalServerProcess`配管）を掃除する。

**Tech Stack:** Unity C# (moorestech_client), UniTask, NUnit, uloop CLI

## Requirements

- ローカルプレイ（StartLocal・プレイテストDSL・StandaloneQa・EditModeInPlayingTest・GameInitializerシーン直接再生）は接続試行なしで必ず内蔵サーバーを起動する。受け入れ基準: 11564に別サーバーが居てもそこへ接続しない
- 内蔵サーバーは `CreateLocalServerArgs` に明示ポートがあればそれを尊重し、無ければ port 0（OS自動採番）で起動し `BoundPort` へ接続する（現行 `Port ??= 0` の維持）
- 外部サーバー接続は `ConnectServer` メニュー（IP:ポート入力）経由の明示指定のみ。受け入れ基準: リモート接続失敗時に内蔵サーバーへフォールバックせず、エラー表示→メインメニューへ戻る
- ローカル起動失敗時もエラー表示→メインメニューへ戻る（現行の最終catchと同じ復帰動作）
- `InitializeProprieties.CreateDefault()` を廃止し、全呼び出し側をファクトリ2本へ一括更新する
- クライアントの11564残骸を削除: `ServerConst.LocalServerPort`、`MainGameStarter` の未使用フィールド（`IPAddress`/`Port`/`isLocal`/`localServerProcess`/`PlayerId`）
- 常時nullの `LocalServerProcess` 配管（`InitializeProprieties.LocalServerProcess`・`VanillaApi` のProcess引数と `Kill()`）を削除する
- 検証: コンパイル＋既存テスト（Playtest系・Boot系）＋unityプレイ録画テストで「11564にダミーリスナーが居る状態でも自前の内蔵サーバーで正常起動する」ことを実証
- 実装完了後、メインクローン直下のローカル運用ドキュメント（`CLAUDE.local.md`・`HANDOFF-2026-08-17-worktree-parallel-ops.md`、いずれもgit管理外）の「クライアントは最初に11564へ接続試行するため誤接続する」注意書きを実態（フォールバック廃止済み）に合わせて書き換える
- やらないこと: エディタ用の外部接続先設定の追加、リトライUI、サーバー側既定ポート11564の変更、unity-playmode-recorded-playtestスキル文書の修正（bd moorestech-vkn 別タスク）

## Global Constraints

- AGENTS.md 全規約に従う: 2行セットコメント（日本語→English、各1行）、`#region Internal` はメソッド内ローカル関数用途のみ、partial禁止、`Func<>`禁止、デフォルト引数禁止、単純getter/setter禁止
- try-catchは外部境界（ネットワーク送受信・外部プロセス起動）の隔離のみ許可し、境界根拠をコメント明記する。SocketException捕捉によるフォールバックは削除対象
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する
- 作業はタスク用worktree（メインクローンからLibraryコピー）で行い、各タスク完了ごとにコミットする
- テスト実行は `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。「Unity is reloading (Domain Reload in progress)」エラー時は45秒待機してリトライ
- .metaファイルは手動作成しない（新規.csはUnityコンパイルで自動生成されたものをコミット）

## File Structure

- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeProprieties.cs` — ファクトリ2本化・モード保持・Process削除
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/ServerConnectionInitializer.cs` — モード分岐・フォールバック削除
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs` — Process引数と`Kill()`削除
- Modify: `moorestech_client/Assets/Scripts/Client.Common/ServerConst.cs` — `LocalServerPort` 削除
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs` — `CreateLocalServer` へ
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs` — `CreateRemoteConnection` へ
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` — 既定値を `CreateLocalServer` へ
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestWorldBootSession.cs` — `CreateLocalServer` へ
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs` — `CreateLocalServer` へ
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs` — `CreateLocalServer` へ
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` — 死にフィールド削除
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Starter/InitializeProprietiesTest.cs` — ファクトリのモード保持を固定するEditModeテスト

## 配置と前例

- モード保持は `Client.Starter/InitializeProprieties.cs` 内に閉じる（新アセンブリ・新層なし）。前例: 起動意図をInitializeProprietiesが運ぶ現行構造そのまま
- ファクトリメソッドパターンの前例: `InitializeProprieties.CreateDefault()`（本plan で2本に置換）
- 失敗時のメインメニュー復帰の前例: `ServerConnectionInitializer.cs:100-105` の現行最終catch（`Debug.LogError`＋`loadingLog`＋`SceneManager.LoadScene(SceneConstant.MainMenuSceneName)`＋throw）を両モードで踏襲
- データフロー: 各起動入口（StartLocal/ConnectServer/Playtest/QA/テストUtil）→ `InitializeProprieties`（共有モデル）→ `ServerConnectionInitializer`（読み手）の一方向は不変。書き手の作り方が変わるだけで交差点の追加はない

---

### Task 1: InitializeProprieties のファクトリ2本化と全呼び出し側の一括更新

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeProprieties.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Common/ServerConst.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/StartLocal.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs:80`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:35,97`
- Modify: `moorestech_client/Assets/Scripts/Client.Playtest/PlaytestWorldBootSession.cs:46`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/StandaloneQa/StandaloneTerrainQaSettings.cs:78`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs:77`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:154-161`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Starter/InitializeProprietiesTest.cs`

**Interfaces:**
- Consumes: `ServerConst.LocalServerIp` / `ServerConst.DefaultPlayerId`（既存）
- Produces: `InitializeProprieties.CreateLocalServer(int playerId)`、`InitializeProprieties.CreateRemoteConnection(string serverIp, int serverPort, int playerId)`、`bool IsRemoteConnection { get; }`（Task 2 が分岐に使用。`LocalServerProcess` と `CreateDefault()` は消滅し、Task 2 で `VanillaApi` 側の受け口も消す）

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Starter/InitializeProprietiesTest.cs` を新規作成:

```csharp
using Client.Common;
using Client.Starter;
using NUnit.Framework;

namespace Client.Tests.Starter
{
    public class InitializeProprietiesTest
    {
        [Test]
        public void CreateLocalServer_ローカルモードで生成しループバックIPを持つ()
        {
            var proprieties = InitializeProprieties.CreateLocalServer(ServerConst.DefaultPlayerId);

            // ローカルは接続試行しないモードとして固定する
            // Pin local play as the mode that never probes an existing server
            Assert.That(proprieties.IsRemoteConnection, Is.False);
            Assert.That(proprieties.ServerIp, Is.EqualTo(ServerConst.LocalServerIp));
            Assert.That(proprieties.PlayerId, Is.EqualTo(ServerConst.DefaultPlayerId));
        }

        [Test]
        public void CreateRemoteConnection_リモートモードで指定IPとポートを保持する()
        {
            var proprieties = InitializeProprieties.CreateRemoteConnection("192.168.1.10", 25000, 5);

            // 外部接続は明示指定の宛先だけを運ぶことを固定する
            // Pin that remote connection carries only the explicitly specified destination
            Assert.That(proprieties.IsRemoteConnection, Is.True);
            Assert.That(proprieties.ServerIp, Is.EqualTo("192.168.1.10"));
            Assert.That(proprieties.ServerPort, Is.EqualTo(25000));
            Assert.That(proprieties.PlayerId, Is.EqualTo(5));
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CreateLocalServer` / `CreateRemoteConnection` / `IsRemoteConnection` が未定義のコンパイルエラー

- [x] **Step 3: InitializeProprieties を書き換える**

`InitializeProprieties.cs` 全体を以下へ置換（`Process`・`CreateDefault`・publicコンストラクタを削除）:

```csharp
using System;
using Client.Common;

namespace Client.Starter
{
    public class InitializeProprieties
    {
        public readonly bool IsRemoteConnection;
        public readonly string ServerIp;
        public readonly int ServerPort;
        public readonly int PlayerId;

        public string[] CreateLocalServerArgs { get; set; } = Array.Empty<string>();

        private InitializeProprieties(bool isRemoteConnection, string serverIp, int serverPort, int playerId)
        {
            IsRemoteConnection = isRemoteConnection;
            ServerIp = serverIp;
            ServerPort = serverPort;
            PlayerId = playerId;
        }

        // ローカルプレイは接続試行なしで内蔵サーバーを必ず起動する（ADR 0013）
        // Local play always boots the embedded server without probing (ADR 0013)
        public static InitializeProprieties CreateLocalServer(int playerId)
        {
            return new InitializeProprieties(false, ServerConst.LocalServerIp, 0, playerId);
        }

        // 外部サーバー接続は明示IP:ポート指定のみ・失敗時フォールバック無し
        // Remote connection only with an explicit IP:port, never falling back
        public static InitializeProprieties CreateRemoteConnection(string serverIp, int serverPort, int playerId)
        {
            return new InitializeProprieties(true, serverIp, serverPort, playerId);
        }
    }
}
```

- [x] **Step 4: ServerConst から LocalServerPort を削除する**

`ServerConst.cs:8` の `public const int LocalServerPort = 11564;` の行を削除する。

- [x] **Step 5: StartLocal をローカル明示へ書き換える**

`StartLocal.cs` の `_serverProcess` フィールド（16行目）と `using System.Diagnostics;`（1行目）を削除し、`OnMainGameSceneLoaded` を以下へ:

```csharp
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();

            starter.SetProperty(InitializeProprieties.CreateLocalServer(PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey)));
        }
```

- [x] **Step 6: ConnectServer をリモート明示へ書き換える**

`ConnectServer.cs:80` を以下へ:

```csharp
            var properties = InitializeProprieties.CreateRemoteConnection(ip, port, playerId);
```

- [x] **Step 7: 残りの CreateDefault 呼び出し4箇所をローカル明示へ書き換える**

すべて `InitializeProprieties.CreateDefault()` → `InitializeProprieties.CreateLocalServer(ServerConst.DefaultPlayerId)` に置換し、ファイルに `using Client.Common;` が無ければ追加する:

- `InitializeScenePipeline.cs:35` のフィールド初期化と `:97` の `??=`
- `PlaytestWorldBootSession.cs:46`
- `StandaloneTerrainQaSettings.cs:78`
- `EditModeInPlayingTestUtil.cs:77`（変数名 `defaultProperties` は `localProperties` へリネーム）

- [x] **Step 8: MainGameStarter の死にフィールドを削除する**

`MainGameStarter.cs:154-161` から未使用フィールド `IPAddress` / `isLocal` / `localServerProcess` / `PlayerId` / `Port` の5つを削除する（`_resolver` は残す）。あわせて未使用になったusing（`System.Diagnostics`、`Client.Common` が他で未使用なら）を削除する。

- [x] **Step 9: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件。ただし Task 2 未着手のため `ServerConnectionInitializer.cs:50` の `_proprieties.LocalServerProcess` 参照でエラーが出る。その場合はこのステップ限りの暫定として同参照を `null` リテラルへ置き（Task 2 で引数ごと削除される）、再コンパイルでエラー0件を確認する

- [x] **Step 10: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InitializeProprietiesTest"`
Expected: 2件PASS

- [x] **Step 11: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat: InitializeProprietiesをローカル/リモートのファクトリ2本へ反転しCreateDefaultと11564既定を廃止"
```

---

### Task 2: ServerConnectionInitializer のフォールバック削除と VanillaApi の Process 配管撤去

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/ServerConnectionInitializer.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs`

**Interfaces:**
- Consumes: `InitializeProprieties.IsRemoteConnection` / `ServerIp` / `ServerPort` / `CreateLocalServerArgs`（Task 1）
- Produces: `VanillaApi` コンストラクタから `Process localServerProcess` 引数が消える（他の呼び出し元は `ServerConnectionInitializer` のみ）

- [x] **Step 1: ServerConnectionInitializer の接続ロジックを反転する**

`ServerConnectionInitializer.cs` の `ConnectionToServer` ローカル関数を以下へ置換し、`using System.Net.Sockets;` を削除する:

```csharp
            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                var timeOut = TimeSpan.FromSeconds(3);

                // リモートは明示指定の宛先のみ。失敗しても内蔵サーバーへフォールバックしない（ADR 0013）
                // Remote uses only the explicit destination and never falls back to the embedded server (ADR 0013)
                if (_proprieties.IsRemoteConnection)
                {
                    // サーバー接続はネットワーク境界のため失敗を隔離しメニューへ復帰する
                    // Server connect is a network boundary; isolate failures and return to the menu
                    try
                    {
                        var serverProperties = new ConnectionServerProperties(_proprieties.ServerIp, _proprieties.ServerPort);
                        return await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                    }
                    catch (Exception e)
                    {
                        await HandleConnectionFailure(e);
                        throw;
                    }
                }

                // ローカルは接続試行なしで必ず内蔵サーバーを起動する
                // Local play always boots the embedded server without probing
                // 内蔵サーバー起動と接続はプロセス/ネットワーク境界のため失敗を隔離しメニューへ復帰する
                // Embedded server launch and connect are process/network boundaries; isolate failures and return to the menu
                try
                {
                    var serverInstanceGameObject = new GameObject("ServerInstance");
                    var serverStarter = serverInstanceGameObject.AddComponent<ServerStarter>();

                    // ポート未指定なら0(OS自動採番)を渡し、実行時に空きポートへバインドさせる
                    // Pass 0 (OS auto-assign) when no port is specified, so the server binds a free port at runtime
                    var localServerSettings = CliConvert.Parse<StartServerSettings>(_proprieties.CreateLocalServerArgs ?? Array.Empty<string>());
                    localServerSettings.Port ??= 0;
                    serverStarter.SetArgs(CliConvert.Serialize(localServerSettings));
                    UnityEngine.Object.DontDestroyOnLoad(serverInstanceGameObject);

                    // バインド完了を待ち、実際に割り当てられたポートへ接続する
                    // Wait for binding to complete, then connect to the actually assigned port
                    await UniTask.WaitUntil(() => serverStarter.BoundPort != 0).Timeout(TimeSpan.FromSeconds(60));
                    var localServerProperties = new ConnectionServerProperties(_proprieties.ServerIp, serverStarter.BoundPort);

                    return await ServerCommunicator.CreateConnectedInstance(localServerProperties).Timeout(timeOut);
                }
                catch (Exception e)
                {
                    await HandleConnectionFailure(e);
                    throw;
                }
            }

            // 失敗をログとUIへ出しメインメニューへ戻す共通復帰処理
            // Shared recovery path: log the failure, show it in the UI, and return to the main menu
            async UniTask HandleConnectionFailure(Exception e)
            {
                Debug.LogError($"サーバーへの接続に失敗しました: {e.Message}");
                _loadingLog.text += "\nサーバーへの接続に失敗しました。メインメニューに戻ります。";
                await UniTask.Delay(2000);
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
            }
```

`HandleConnectionFailure` は `ConnectionToServer` と同じ `#region Internal` 内に置く。

- [x] **Step 2: VanillaApi から Process 配管を削除する**

`VanillaApi.cs` で以下を行う（`StartLocal._serverProcess` が常時nullだったため配管全体が死にコード）:
- `using System.Diagnostics;` を削除
- フィールド `private readonly Process _localServerProcess;` を削除
- コンストラクタ引数 `Process localServerProcess` と代入 `_localServerProcess = localServerProcess;` を削除
- `Disconnect()` 内の `_localServerProcess?.Kill();` を削除

`ServerConnectionInitializer.cs:50` の `VanillaApi` 生成を引数4つへ:

```csharp
            var vanillaApi = new VanillaApi(exchangeManager, packetSender, serverCommunicator, _playerConnectionSetting);
```

（Task 1 Step 9 で暫定 `null` を入れた場合はここで消える）

- [x] **Step 3: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [x] **Step 4: 既存テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestBootLifecycleTest|PlaytestWorldBootSessionTest|StandaloneTerrainQaSettingsTest|InitializeProprietiesTest"`
Expected: 全件PASS（Domain Reloadエラー時は45秒待機してリトライ）

- [x] **Step 5: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "feat: ローカルプレイの接続試行フォールバックを廃止し必ず内蔵サーバーを起動する (ADR 0013)"
```

---

### Task 3: unityプレイ録画テストで11564誤接続の根絶を実証する

**Files:**
- 変更なし（検証のみ。録画・result.jsonは成果物として確認する）

**Interfaces:**
- Consumes: Task 2 までの反転済みクライアント

- [x] **Step 1: 11564にダミーリスナーを立てる**

旧コードなら誤接続していた事故条件を再現する。バックグラウンドで:

```bash
python3 -c "
import socket
s = socket.socket(); s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
s.bind(('127.0.0.1', 11564)); s.listen(5)
print('dummy listening on 11564', flush=True)
while True:
    c, _ = s.accept(); print('UNEXPECTED CONNECTION', flush=True)
" &
```

Expected: `dummy listening on 11564` が出力される

- [x] **Step 2: unity-playmode-recorded-playtest スキルでシナリオを1本実行する**

unity-playmode-recorded-playtest スキルを起動し、プレイテストDSL（`scripts/run-scenario.sh`）で最小シナリオ（ゲーム起動→ワールド表示確認程度）を実行する。masterデータworktreeピン留め等の手順はスキルに従う。

Expected: result.json が success。ダミーリスナー側に `UNEXPECTED CONNECTION` が**出力されない**（=11564へ一切接続していない）

- [x] **Step 3: ダミーリスナーを終了し、結果を記録する**

```bash
kill %1
```

result.json の成否とダミーリスナー出力の有無を bd へ記録する:

```bash
bd note moorestech-kjp "録画テスト実証: 11564ダミーリスナー稼働下でresult.json success・ダミーへの接続ゼロを確認"
```

- [x] **Step 4: ローカル運用ドキュメントの11564注意書きを更新する**

メインクローン直下の2ファイル（いずれもgit管理外・コミット不要）を実態に合わせて書き換える:

- `CLAUDE.local.md`（「## 運用上の注意」節） — 「ただしクライアントは最初に11564へ接続を試みるので、スタンドアロンサーバーをポート未指定（既定11564）で立てっぱなしにすると他worktreeのPlayModeがそこへ繋がってしまう。スタンドアロン起動時は`--port`を明示すること。」の部分を以下へ置換:
  「なおADR 0013の反転済みのため、ローカルプレイは接続試行なしで必ず内蔵サーバーを起動する。スタンドアロンサーバーが11564に居ても誤接続しない（外部接続はConnectServerメニューの明示指定のみ）。」
- `HANDOFF-2026-08-17-worktree-parallel-ops.md:53` — 「残る注意は『クライアントが最初に11564へ接続試行するため…』。kjp完了後はこの注意書きも消せる」を「kjp実装済み（ADR 0013）: クライアントは11564へ接続試行しないため、この注意書きは不要になった」へ置換

- [x] **Step 5: moorestech-vkn へ前提変更を記録する**

```bash
bd note moorestech-vkn "kjp実装完了(ADR 0013): クライアントは11564へ接続試行しなくなったため、スキル文書の書き換えでは『ポート未指定スタンドアロンサーバーへの誤接続』注意書き自体が不要。『内蔵サーバーはport 0(OS採番)→BoundPort接続で並列可』のみを正として記述する"
```

- [x] **Step 6: コミットする**

シナリオファイル等の成果物を追加した場合のみ:

```bash
git add -A
git commit -m "test: 11564ダミーリスナー稼働下でのローカル起動実証シナリオ"
```

---

### Task 4: 全ブランチレビュー（省略不可）

- [x] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。moorestechプロジェクトのため moores-code-review を使う。

- [x] **Step 2: レビュー指摘の機械的修正を適用し、コンパイル・テストを再実行してコミットする**

```bash
uloop compile --project-path ./moorestech_client
git add -A && git commit -m "fix: レビュー指摘対応"
```

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0013-local-play-always-boots-embedded-server.md`（ファクトリ2本化・外部接続はConnectServerのみ・失敗時フォールバック無し・11564残骸掃除・録画テスト実証。全てユーザー裁定 2026-08-17）
- 裁定レコード: `.decisions/2026-08-17-ローカルプレイは接続試行なしで必ず内蔵サーバーを起動する.md`
- planning中の追加判断:
  - 常時nullの `LocalServerProcess` 配管（VanillaApiのProcess引数・`Kill()`）も削除対象に含める。出所: agent前提（`StartLocal._serverProcess` が一度も代入されず全経路でnullである事実に基づく死にコード掃除。Q4「11564残骸と死にフィールドの同時掃除」裁定の延長）
  - モード表現は型分割でなく `bool IsRemoteConnection`＋privateコンストラクタ＋ファクトリ2本。出所: ユーザー裁定 2026-08-17（Q1でA案）
  - 失敗復帰処理は `HandleConnectionFailure` ローカル関数へ共通化。出所: agent前提（現行catch内復帰コードの重複排除、挙動は現行踏襲）
  - 録画テストのダミーは実サーバーでなくpython素TCPリスナー。出所: agent前提（旧コードなら接続してハンドシェイクで死ぬ条件を最小コストで再現でき、接続の有無を直接観測できるため）
