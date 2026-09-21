# 正常終了の印を終了パイプライン自身が書く Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** エディタでPlayを停止しただけで次回起動に「前回異常終了の確認」ゲートが出る問題を、終了パイプライン自身が正常終了の印を書く保険を持つことで根治する。

**Architecture:** `GameShutdownEvent` が `Application.quitting` を購読し、誰も終了の意思を表明しないまま終了要求が来たときだけ自分で `UnawaitableExit` を発火する。これにより `CleanExitMarkWriter` が正常終了の印を書く経路が、MainGameシーン上の `SaveAndQuitPresenter` の生存と例外の有無から切り離される。あわせて `SaveAndQuitPresenter.Disconnect()` が初期化前に投げていた NullReferenceException を潰す。`PreviousSessionStartupTasks.BeginCurrentSessionMarks()` が最初のawait前に今回sessionとwriterを同期設置し、ホスト起動後のsalvage/recoveryから分離する。終了印はWebUi未ready・起動失敗・remoteでも持ち、同意に依存する記録収集の条件とは分ける。

**Tech Stack:** Unity 6 / C# / UniTask / UniRx / NUnit（EditMode）/ uloop CLI

## 出所とreview r2の更新

元台帳のユーザー原文「プレイを停止しただけなのにこれが出るのうざい」が支持するのは、通常停止を偽クラッシュにしない要求。具体API・所有者・検証方式の個別承認は一次記録を再確認できないため、以下は明示された自律実行委任と正本要件に基づくagent設計判断とする。controllerはD1案A（最早期に終了印を設置）、D2案B（未確認の具体裁定をagent前提へ分離）、D3案B（既存3 API維持）を採用した。チェックボックスの状態はcontrollerが管理する。

## Requirements

- R1: 未宣言の通常終了としてエディタでPlayを停止したとき、初期化の途中でもゲート応答待ちでも、必ず正常終了の印（`last-session/marks/pid_<PID>/session_<utcTicks>/clean`）が書かれること。受け入れ基準: 起動直後（MainGameシーン読込前）に停止 → 再Play したとき「PlaytestStartGates: 前回異常終了の確認の応答を待ちます」がログに出ず、起動がゲートで止まらない。
- R2: 正常終了の印を書く責任は `GameShutdownEvent` が持ち、`Application.quitting` 購読による保険として実装すること。受け入れ基準: `SaveAndQuitPresenter` が存在しない状態の終了でも印が書かれる。
- R3: 正規の終了口（`QuitApplicationAsync`）を通った終了では保険が何もしないこと。受け入れ基準: `_fired` が立っている状態で保険を叩いても二重発火せず、終了処理中の停止が「異常終了」として数えられる従来の検知（F03）が保たれる。
- 既宣言の例外: IntentionalExitのflush中はR3/F03を維持し、InitializationFailed通知済みの停止は失敗判定を維持する。「必ず」を既宣言の失敗の正常化には用いない。
- R4: 保険が発火したことを開発者が読めるログに必ず出すこと。受け入れ基準: 保険経路を通った起動の Editor.log に、意思表明が無いまま終了要求が来た旨のログが1行出る。
- R5: `SaveAndQuitPresenter.Disconnect()` が初期化前（`ClientContext.VanillaApi` 未設定）に NullReferenceException を投げないこと。受け入れ基準: 起動直後に停止しても Editor.log に `SaveAndQuitPresenter.Disconnect` 由来の NRE が出ない。
- R6: 保険の分岐（未発火なら発火する／発火済みなら何もしない）が EditMode テストで固定されていること。
- R7: 配布ビルドの既存挙動（`Application.wantsToQuit` による終了要求の保留と、正規口を通る終了）を壊さないこと。受け入れ基準: 既存の `GameShutdownFlushTest` が通り続ける。
- やらないこと: 開始ゲートの仕組み自体の変更（エディタでゲートを出さなくする・起動を止めないようにする、はいずれも棄却済み）。`UIStateControl.Update` 等の他の未初期化 NRE（`moorestech-johg.2`）の修正。バグ報告のアップロード判定の変更。

## Global Constraints

- コメントは日本語・英語の2行セット（`// 日本語` → `// English`）を約3〜10行ごとに入れる。各言語1行に収め、折り返さない。自明なコメントは書かない。
- `try-catch` は外部境界の隔離目的のみ。本planの変更箇所では新規に使わない。
- `Func<>` 禁止・`partial` 禁止・デフォルト引数禁止。
- fail-closed で何もしない縮退経路は、必ず理由を開発者が読めるログへ出す（無音の縮退は禁止）。
- エディタ専用コードは `#if UNITY_EDITOR` で囲みファイル末尾に置く（本planでは新規のエディタ専用コードは作らない）。
- イベント/通知の標準は UniRx。C# の `event Action` は使わない（本planでは Unity の `Application.quitting` を購読するのみで、新規イベントは作らない）。
- 1ファイル200行以下。`.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。
- 作業ブランチは `fix/editor-stop-clean-exit-mark`（`master` 27c3a9e42 起点）。

---

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs` | Modify | 終了パイプライン。`Application.quitting` 購読の保険を追加する |
| `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` | Modify | 起動シーケンス。保険と今回session/writerを最初のawait前に据える |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveAndQuitPresenter.cs` | Modify | 破棄・終了時の切断。NRE を潰し、通知が切断の失敗に巻き込まれないようにする |
| `moorestech_client/Assets/Scripts/Client.Tests/Starter/GameShutdownFlushTest.cs` | Modify | 保険の分岐テストを追記する（同種のテストが既にここにある） |
| `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PreviousSessionStartupTasks.cs` | Modify | 今回印の同期開始と後段salvage/recoveryを分離する |
| `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestRecordCollection.cs` | Modify | 収集同意の対象から終了印を分離しログを整合する |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/CleanExitMarkWriter.cs` | Modify | 設置契約のコメントを最初のawait前へ整合する |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Salvage/PreviousSessionSalvage.cs` | Modify | 先行印生成で成立しなくなるfirstBootディレクトリ判定ログを除く |
| `moorestech_client/Assets/Scripts/Client.Game/Common/ShutdownFlushResultAggregator.cs` | Create | 200行制約のため既存集約とmasked failure報告を抽出、専用処理はローカル関数に閉じる |
| `moorestech_client/Assets/Scripts/Client.Tests/Starter/PreviousSessionStartupTasksTest.cs` | Create | WebUi/remote条件に依存しない同期印・再Play時のsession分離を固定する |

### 配置と前例

- 保険の置き場は `Client.Game/Common/GameShutdownEvent.cs`。終了の意思表明を所有しているのはこのクラスであり、`_fired` による重複判定もここにしか無い（前例: 同ファイルの `InstallApplicationQuitDeferral`／`OnApplicationWantsToQuit` が既に Unity のアプリ終了フックを購読している）。
- `Application.quitting` の購読は `-=` してから `+=` する形にする（前例: `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs:36-37`）。
- テストの置き場は `Client.Tests/Starter/GameShutdownFlushTest.cs`。`GameShutdownEvent` の分岐テストが既にここにあり、`Client.Game` は `InternalsVisibleTo("Client.Tests")` を宣言しているので `internal` メソッドを直接叩ける（`moorestech_client/Assets/Scripts/Client.Game/AssemblyInfo.cs`）。
- 新規パターンは無い。新規イベント・新規プロトコル・新規アセンブリ参照はいずれも作らない。

### データフロー地図

```
Play停止／プロセス終了要求
  → Application.quitting（Unity）
  → GameShutdownEvent（終了の意思表明の唯一の持ち主。未発火のときだけ UnawaitableExit を発火）
  → ［OnGameShutdown］
  → CleanExitMarkWriter（読み手）→ clean の印をディスクへ
  → 次回起動の PreviousSessionSalvage が印を読む → ゲートを出すかが決まる
```

通知は既存の共有モデル（`GameShutdownEvent`）へ集約し、分岐・逆流・並行終了経路は足さない。review r2ではwriterの購読内容は維持したまま設置時点を最初のawait前へ移す。salvage/recoveryは現在のsession識別を再作成せず、既存scannerが今回の印を除外する。ADR 0060裁定5の同時設置は時点のみ部分更新する。

### 機能パリティ（死活表）

| 現在できること | 計画後も生きるか | 根拠 |
|---|---|---|
| ポーズメニューの「セーブして終了」 | 生きる | `QuitApplicationAsync` 経路は無変更。先に `_fired` が立つので保険は何もしない |
| 配布ビルドのウィンドウ×による終了（書き出しを待つ） | 生きる | `Application.wantsToQuit` の保留は無変更。保留 → `QuitApplicationAsync` → `_fired` の順で、保険は後から何もしない |
| 終了処理中に止まった場合を異常終了として検知（F03） | 生きる | 意思表明済み＝`_fired` が立っているので保険は発火せず、`exit_intent` だけが残る従来の判定になる |
| 無人起動（テスト・録画DSL）のPlay停止 | 生きる（印が書かれるようになる） | ゲート自体は無人起動の印で従来どおりスキップされる |
| エディタでEditorごとクラッシュした場合のゲート | 生きる | ハードクラッシュでは `Application.quitting` が飛ばないので印は書かれない |

---

## Task 1: 終了パイプラインに「意思表明の無い終了」の保険を足す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:56-57`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Starter/GameShutdownFlushTest.cs`

**Interfaces:**
- Consumes: 既存の `GameShutdownEvent.ResetForNewSession()`, `GameShutdownEvent.FireGameShutdown(GameShutdownReason)`, `GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason)`, `GameShutdownEvent.OnGameShutdown`
- Produces:
  - `public static void GameShutdownEvent.InstallUnannouncedExitNotice()` — 引数なし・戻り値なし。`Application.quitting` へ保険を据える。二重呼び出し安全
  - `internal static bool GameShutdownEvent.NotifyUnannouncedExit()` — 引数なし。未発火なら `UnawaitableExit` を発火して `true`、発火済みなら何もせず `false`

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Starter/GameShutdownFlushTest.cs` の `GameShutdownFlushTest` クラス内、既存の `FireGameShutdownAsync_SecondFireIsAlreadyShutdown` の**直後**に以下の2テストを追加する（`private class ControllableShutdownParticipant` の定義より前に置く）。

```csharp
        [Test]
        public void NotifyUnannouncedExit_FiresUnawaitableExitWhenNobodyDeclared()
        {
            GameShutdownEvent.ResetForNewSession();
            var notifiedReason = GameShutdownReason.IntentionalExit;
            var notifiedCount = 0;
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(reason =>
            {
                notifiedReason = reason;
                notifiedCount++;
            });

            Assert.IsTrue(GameShutdownEvent.NotifyUnannouncedExit(), "意思表明の無い終了で保険が発火していない");
            Assert.AreEqual(1, notifiedCount);
            Assert.AreEqual(GameShutdownReason.UnawaitableExit, notifiedReason);
        }

        [Test]
        public void NotifyUnannouncedExit_DoesNothingAfterCanonicalExitDeclared()
        {
            GameShutdownEvent.ResetForNewSession();
            var notifiedCount = 0;
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(_ => notifiedCount++);

            var participant = new ControllableShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);
            var shutdown = GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);

            // 意思表明済みの終了では保険は何もしない。ここで発火すると終了処理中の停止が正常終了に化ける
            // The fallback stays silent once the exit was declared; firing here would disguise a stall during shutdown as a clean exit
            Assert.IsFalse(GameShutdownEvent.NotifyUnannouncedExit(), "意思表明済みなのに保険が二重発火している");
            Assert.AreEqual(1, notifiedCount);

            participant.Complete(ShutdownFlushResult.Flushed);
            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown.GetAwaiter().GetResult());
        }
```

ファイル先頭に `using UniRx;`（`IObservable<T>.Subscribe` 拡張用）を追加する。`using var`は構文なので`using System;`は不要（review r2 C4）。

```csharp
using Client.Game.Common;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: FAIL. `CS0117: 'GameShutdownEvent' does not contain a definition for 'NotifyUnannouncedExit'`

- [x] **Step 3: 保険を実装する**

`GameShutdownEvent.cs` を2箇所編集する。フィールドは追加しない（重複購読は `-=`／`+=` で防ぐため、据え付け済みフラグは持たない）。

(3-1) `InstallApplicationQuitDeferral()` の中の、Editor を弾くコメントを実態へ合わせて書き換える。メソッド全体を次に置き換える。

```csharp
        // ウィンドウを閉じる等のOS由来の終了要求を一度止め、書き出しを待つ正規の終了口へ流す。止めないと完了前にプロセスが消える
        // Holds OS-originated quit requests (closing the window) and routes them through the awaiting exit; otherwise the process dies before the flush
        public static void InstallApplicationQuitDeferral()
        {
            // Editorの終了要求を止めるとEditor自体が閉じられなくなる。Editorでの停止は待たずに InstallUnannouncedExitNotice の保険が記録する
            // Holding the Editor's own quit would keep the Editor from closing; an Editor stop is instead recorded, without waiting, by InstallUnannouncedExitNotice
            if (Application.isEditor || _quitDeferralInstalled) return;
            _quitDeferralInstalled = true;
            Application.wantsToQuit += OnApplicationWantsToQuit;
        }
```

(3-2) `InstallApplicationQuitDeferral()` の直後（`private static bool OnApplicationWantsToQuit()` の前）に、保険の据え付けと本体を挿入する。

```csharp
        // 終了の意思表明が誰からも出ないまま終わる経路の保険。エディタのPlay停止では、ここだけが終了を知らせる
        // The fallback for exits nobody declares; on an Editor play-stop this is the only thing that announces the shutdown
        // シーン上のオブジェクトの生存に依らない。初期化の途中やゲート応答待ちで止めても、正常終了の印が残る
        // It does not depend on any scene object being alive, so a stop mid-initialization or at the start gate still leaves the clean-exit mark
        public static void InstallUnannouncedExitNotice()
        {
            // 起動シーケンスは再入する（Editorの再生し直し）。重複購読を機械的に防ぐ
            // The boot sequence re-enters (an Editor replay), so a duplicate subscription is ruled out mechanically
            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;

            #region Internal

            static void OnApplicationQuitting()
            {
                NotifyUnannouncedExit();
            }

            #endregion
        }

        // 誰も意思表明していない終了だけを拾う。正規の終了口を通った終了は既に_firedが立っており、終了処理中の停止の検知を潰さない
        // Picks up only the exits nobody declared; a canonical exit already set _fired, so the detection of a stall during shutdown stays intact
        internal static bool NotifyUnannouncedExit()
        {
            if (_fired)
            {
                Debug.Log("終了の意思表明は既に通知済みのため、保険の終了通知は行いません");
                return false;
            }
            Debug.Log("終了の意思表明が無いまま終了要求が来たため、待てない終了として記録します（エディタのPlay停止・OS由来の終了）");
            FireGameShutdown(GameShutdownReason.UnawaitableExit);
            return true;
        }
```

- [x] **Step 4: 起動シーケンスへ据え付けを追加する**

`moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` の56-57行目を次に置き換える。

```csharp
            GameShutdownEvent.ResetForNewSession();
            GameShutdownEvent.InstallApplicationQuitDeferral();

            // 正規の終了口を通らない終了（エディタのPlay停止）でも、正常終了の印が書かれるようにする
            // Ensures the clean-exit mark is written even for exits that skip the canonical path (an Editor play-stop)
            GameShutdownEvent.InstallUnannouncedExitNotice();
            Playtest.PreviousSessionStartupTasks.BeginCurrentSessionMarks();
```

review r2 D1補完: 先に `PreviousSessionStartupTasksTest` を追加し、未実装 `BeginCurrentSessionMarks` のCS0117をREDとして確認する。次に同メソッドへ `ProcessSessionScope.BeginNewSession()` と現在pid/sessionでの `CleanExitMarkWriter.InstallAtStartup` を移す。後段の `RunAtStartup` はsalvage/recoveryのみとし、session再開始・writer再設置を除く。WebUiのreadyとremoteの4組合せ、同pid再Playの旧/現session分離を5ケースで固定する。共有資料を全消去せず生成sessionのみ片付ける。

- [x] **Step 5: コンパイルとテストを実行して通ることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GameShutdownFlushTest"`
Expected: 4テストすべて PASS（既存2＋新規2）

- [x] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs moorestech_client/Assets/Scripts/Client.Tests/Starter/GameShutdownFlushTest.cs
git commit -m "$(cat <<'EOF'
fix: 意思表明の無い終了でも正常終了の印を書く保険を終了パイプラインへ置く

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: 初期化前の切断で NullReferenceException を投げないようにする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveAndQuitPresenter.cs:38-45`

**Interfaces:**
- Consumes: Task 1 で追加した保険（このタスクの修正が無くても印は残る前提に立てる）、既存の `ClientContext.VanillaApi`, `GameShutdownEvent.FireGameShutdown(GameShutdownReason)`
- Produces: なし（private メソッドの修正のみ）

- [x] **Step 1: Disconnect の順序と未初期化ガードを直す**

`SaveAndQuitPresenter.cs` の `Disconnect()` を次に置き換える。

```csharp
        // 通信の切断は終了経路と破棄経路の双方から来る。終了通知は既発火なら無視される
        // Teardown reaches here from both the exit and destroy paths; an already-fired shutdown notice is ignored
        // ここへ来るのは待てない経路だけ（ビルドのウィンドウ閉じは終了要求の保留で待つ正規口を先に通る）
        // Only unawaitable paths arrive here; a build's window close already went through the awaiting exit via the quit deferral
        private void Disconnect()
        {
            // 通知が先。切断で例外が出ても終了の意思表明が飛ばない状態を作らない
            // The notice goes first so a failing disconnect can never strand the declared exit
            GameShutdownEvent.FireGameShutdown(GameShutdownReason.UnawaitableExit);

            // 接続の確立前に破棄されるとAPIはまだ居ない（起動途中のPlay停止）。切断する相手が居ないので何もしない
            // Destroyed before the connection is established (a play-stop mid-boot) leaves no API, so there is nothing to disconnect
            if (ClientContext.VanillaApi == null)
            {
                Debug.Log("サーバーとの接続が確立する前に破棄されたため、切断は行いません");
                return;
            }
            ClientContext.VanillaApi.Disconnect();
        }
```

- [x] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [x] **Step 3: 周辺テストが壊れていないことを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "GameShutdownFlushTest|Client.Tests.BugReport|Client.Tests.Starter"`
Expected: すべて PASS

追加関連テスト: `PreviousSessionStartupTasksTest|ProgressSessionRecoveryTest`。同じregexへ含めてもよい。

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveAndQuitPresenter.cs
git commit -m "$(cat <<'EOF'
fix: 接続確立前の破棄でSaveAndQuitPresenterがNullReferenceExceptionを投げないようにする

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: 実機Playで印とゲートの挙動を確かめる

**Files:**
- 変更なし（検証のみ。結果は `moorestech-ot11` の bd note に残す（このmachineには元の`moorestech-6git`が無い））

**Interfaces:**
- Consumes: Task 1・Task 2 の実装
- Produces: なし

前提: このタスクは Unity Editor の GUI を動かす。画面ロック中は `uloop` が無言でハングするので、画面のロックを解除してから実行すること。

- [x] **Step 1: 前回セッションの残骸を空にする**

```bash
test -d "/Users/sakastudio/Library/Application Support/moorestech/BugReports/last-session"
session_backup_dir=$(mktemp -d "/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/editor-stop-clean-exit-mark/.superpowers/sdd/task3-backup.XXXXXX")
mv "/Users/sakastudio/Library/Application Support/moorestech/BugReports/last-session" "$session_backup_dir/last-session"
ls "/Users/sakastudio/Library/Application Support/moorestech/BugReports/"
```
Expected: 検証済み絶対パスの`last-session`が無く、旧資料は退避先から復元できる状態。初めから存在しなければ移動は不要。再帰削除は使わない。

- [x] **Step 2: Playを開始し、初期化が終わる前に停止する**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Play
uloop control-play-mode --project-path ./moorestech_client --action Stop
```
（`play` の直後に `stop` を出す。writer設置・started生成・WebUi readyを待たず、最早期停止を検証する）

- [x] **Step 3: 保険が発火し、正常終了の印が書かれたことを確認する**

```bash
grep -c "終了の意思表明が無いまま終了要求が来たため" "$HOME/Library/Logs/Unity/Editor.log"
find "$HOME/Library/Application Support/moorestech/BugReports/last-session/marks" -name clean
```
Expected: 保険ログが1件、今回の新しいpid/sessionに`started`と`clean`がある。前回の残存cleanを証拠にしない。ログファイルがrotate済みなら対象EditorのConsoleを区間分離して同じ文字列を確認する。

```bash
grep -c "SaveAndQuitPresenter.Disconnect" "$HOME/Library/Logs/Unity/Editor.log"
```
Expected: 0（NREが出ていない）

- [x] **Step 4: 再度Playし、ゲートで止まらないことを確認する**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Play
```
起動が進むのを待ってから、拒否・喪失側の語でログを引く。

```bash
grep -E "前回異常終了の確認の応答を待ちます|clean:False|前回セッションの退避で欠損|確認せずに開始します" "$HOME/Library/Logs/Unity/Editor.log" | tail -20
grep "前回セッションの退避が終わりました" "$HOME/Library/Logs/Unity/Editor.log" | tail -1
```
Expected: 1つ目のgrepは今回の起動区間で0件、2つ目の最終行が `clean:True`

- [x] **Step 5: 停止して結果を記録する**

```bash
uloop control-play-mode --project-path ./moorestech_client --action Stop
bd note moorestech-ot11 "実機確認: 初期化途中で停止 → clean の印あり・保険のログあり・SaveAndQuitPresenter の NRE なし。再Playで clean:True・ゲート無しを確認"
```

---

## Task 4: 全ブランチのコードレビュー（省略不可）

**Files:**
- レビュー結果に応じて Task 1〜3 の対象ファイル

**Interfaces:**
- Consumes: Task 1〜3 の全コミット
- Produces: なし

- [x] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master..fix/editor-stop-clean-exit-mark` の差分をレビューする。ゴール文言による省略は不可。

- [x] **Step 2: 指摘を反映する**

指摘を反映する。反映が判定経路・条件式・その評価時点（`_fired` の判定、`Application.quitting` の購読位置、`VanillaApi == null` の判定）に触れた場合は、**Task 3 の実機検証を反映後のコードで最初からやり直してから**このタスクを完了させる。テストの通過・ログの無音は代替にならない。

- [x] **Step 3: 残課題を1件ずつ起票する**

plan・実機検証記録・レビュー結果に書いた「未検証」「未確認」「残差」を、1件ずつ `bd create` で起票する。結論には issue 番号を列挙する（「残差は○○のみ」と要約しない）。起票が済んでいない残課題は残課題と呼ばない。

- [x] **Step 4: コミットしてPRを作る**

```bash
uloop compile --project-path ./moorestech_client
git add -A
git commit -m "$(cat <<'EOF'
docs: ADR 0067と実装planを追加

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>
EOF
)"
```
`pr-create` スキルで PR を作成する。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0067-shutdown-pipeline-marks-clean-exit-without-scene-objects.md`
- 裁定台帳: `.decisions/2026-09-21-正常終了の印は終了パイプライン自身が書く.md`

planning 中に生じた判断:

- **保険の据え付けは `InstallApplicationQuitDeferral` に畳まず、`InstallUnannouncedExitNotice` として別メソッドで `InitializeScenePipeline` から呼ぶ。** 既存メソッドはエディタで早期 return するため、そこへ混ぜると「Editorでは据え付けない」挙動に引きずられる。名前と実処理を一致させる規約にも沿う。
  出所: agent前提（AGENTS.md「名前は実処理と一致させる」・`GameShutdownEvent.InstallApplicationQuitDeferral` の既存の early return）
- **`Application.quitting` の購読は `-=` してから `+=` する。** 起動シーケンスは再入する（Editorの再生し直し・ドメインリロード無効設定）ため、重複購読を機械的に防ぐ。
  出所: agent前提（前例 `Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs:36-37`）
- **保険の本体は `internal static bool NotifyUnannouncedExit()` として切り出す。** `Application.quitting` はテストから発火できないため、分岐だけを直接叩けるようにする。`Client.Game` は `InternalsVisibleTo("Client.Tests")` を宣言済みで、新たな公開面を増やさずに済む。
  出所: agent前提（正本の検証要件と`Client.Game/AssemblyInfo.cs`の既存宣言。具体検証方式へのユーザー個別承認は未確認）
- **`SaveAndQuitPresenter.Disconnect()` は通知を先に出し、その後に未初期化ガード付きで切断する。** 保険があるので順序だけでも印は残るが、通知が切断の失敗に巻き込まれない形にしておく方が意図に合う。ガードの縮退理由はログへ出す。
  出所: agent前提（AGENTS.md「無音の縮退は禁止」）
- **終了分岐のテストは既存の `GameShutdownFlushTest.cs` へ追記し、r2の起動印回帰は `PreviousSessionStartupTasksTest.cs` へ置く。** 同じクラスの分岐テストが既にそこにあり、1ディレクトリ10ファイル・1ファイル200行の制約にも収まる。
  出所: agent前提（既存 `Client.Tests/Starter/GameShutdownFlushTest.cs` の前例）

- **review r2の設計判断:** D1案Aで最初のawait前にsession/writerを確立し、収集同意から終了印を分離する。D2案Bで未確認の具体採択はagent前提とする。D3案Bでshutdown初期化3 APIを維持する。出所: 明示された自律実行委任の下で正本要件を適用したcontroller判断。個別ユーザー承認ではない。
