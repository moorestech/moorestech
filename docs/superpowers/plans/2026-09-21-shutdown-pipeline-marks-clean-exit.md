# Editor Stop Clean Exit Mark Implementation Plan

> **For agentic workers:** This plan is executed by the controller through `subagent-driven-development`. Do not start another SDD session, dispatch subagents, run the final branch review, create a PR, or edit this plan's checkboxes.

**Goal:** Unity EditorのPlay停止を既存shutdownパイプラインへ接続し、記録対象セッションが次回起動で誤って異常終了扱いされないようにする。

**Architecture:** `WebUiHostEditorCleanup`が既に受け取る`ExitingPlayMode`をEditor停止の単一入口として使い、Web UIの同期停止より前に`GameShutdownEvent.FireGameShutdown(GameShutdownReason.UnawaitableExit)`を発火する。clean markerの知識はEditor層へ持ち込まず、既存の`CleanExitMarkWriter`購読に委ねる。

**Tech Stack:** Unity 6、C#、UniRx、NUnit、uloop。

## Requirements

- R1: `EditorApplication.playModeStateChanged`の`ExitingPlayMode`でshutdownパイプラインへ`UnawaitableExit`を1回流す。
- R2: shutdown通知は`WebUiHost.StopAndWaitSync`より前に行い、既存のWeb UI購読とclean marker購読へ停止前に通知する。
- R3: `ExitingPlayMode`以外の状態ではshutdownパイプラインを発火しない。
- R4: Editor停止はawaitできないため、既存仕様どおりflush完了を待たず意思表明時点でclean exitを記録する。
- R5: `InitializationFailed`を異常終了として扱う既存仕様、配布ビルドのawait可能な終了経路、二重発火防止を変えない。
- R6: 実際のEditor callback本体を呼ぶEditModeテストで、`ExitingPlayMode`がclean markerを作ることを固定する。

## Global Constraints

- AGENTS.mdの規約を全て守る。特に200行上限、日英2行コメント、fail-closedのログ、`Func<>`禁止、`partial`禁止を守る。
- Unity固有YAMLと`.meta`は手編集しない。新規`.meta`はUnity生成物だけをコミットする。
- 新しいclean marker経路・Editor専用の直接書き込み・新しい終了理由は作らない。
- `GameShutdownEvent`の汎用層へEditorやプレイテストの語彙を追加しない。
- セーブ形式は変更しない。マイグレーションは不要。
- 実装はテスト先行とし、変更後に必ずUnityコンパイルを実行する。
- 基準測定済み: `CleanExitMarkerTest|GameShutdownFlushTest`は10/10 PASS。

## 配置と前例

データフロー: `EditorApplication.playModeStateChanged` → `WebUiHostEditorCleanup`（書き手）→ `GameShutdownEvent` → `CleanExitMarkWriter`（読み手）→ `CleanExitMarker`。

- Editor停止通知の既存入口: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHostEditorCleanup.cs`
- 待てない終了の既存契約: `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs`、`GameShutdownReason.UnawaitableExit`
- clean markerの既存購読: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/Marks/CleanExitMarkWriter.cs`
- 待てない終了の既存テスト: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/LastSession/CleanExitMarkerTest.cs`
- テストからinternalへ到達する前例: `Client.WebUiHost/AssemblyInfo.cs`の`InternalsVisibleTo("Client.Tests")`

機能死活表:

| 操作・終了経路 | plan後 | 根拠 |
|---|---|---|
| EditorのPlay停止 | 改善 | `ExitingPlayMode`から`UnawaitableExit`を流し、意思表明時点でcleanを記録する |
| 初期化失敗による折り畳み | 維持 | 既存`InitializationFailed`経路は変更せず、clean markerを書かない |
| 配布ビルドのウィンドウ閉じ・ポーズメニュー終了 | 維持 | `InstallApplicationQuitDeferral`と`QuitApplicationAsync`は変更せずflush完了を待つ |
| Web UIのPlay停止時クリーンアップ | 維持 | 既存`CleanupAllSync`を同じcallback内でshutdown通知後に実行する |
| assembly reload時のWeb UIクリーンアップ | 維持 | `AssemblyReloadEvents.beforeAssemblyReload += CleanupAllSync`は変更しない |
| Play外でのEditor終了時Web UIクリーンアップ | 維持 | `EditorApplication.quitting += CleanupAllSync`は変更しない |

配置表:

| 項目 | 配置先 | 機構 |
|---|---|---|
| Editor停止のshutdown通知 | `Client.WebUiHost/Boot/WebUiHostEditorCleanup.cs` | `EditorApplication.playModeStateChanged`から既存static eventへ書く |
| callback統合テスト | `Client.Tests/WebUi/Boot/WebUiHostEditorCleanupTest.cs` | internal callbackを直接呼ぶEditMode NUnit |

## 判断記録（ADR）

- [[2026-09-21-別PCの未同期planは現repoの証拠から再構成する]]
- [[2026-09-21-Editor停止は既存shutdownパイプラインへ接続する]]
- `a8fdccde2`: Editor停止・破棄を`UnawaitableExit`として意思表明時点でcleanにする既存契約。
- `docs/superpowers/plans/2026-09-13-playtest-g-report-kind-crash-and-progress-record.md:3625`: Play Stopがshutdownパイプラインを通らず、次回起動が常に異常終了扱いになる実測。
- `WebUiHostEditorCleanup`からmarkerを直接書かない。汎用shutdownイベントを正として既存購読を活かす。
- `SaveAndQuitPresenter.OnDestroy`だけに依存しない。ロード中には同コンポーネントが存在せず、Editor停止フックの方がセッション全区間を覆う。

### Task 1: Editor Play停止をshutdownパイプラインへ接続する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHostEditorCleanup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Boot/WebUiHostEditorCleanupTest.cs`

**Interfaces:**
- `WebUiHostEditorCleanup.OnPlayModeStateChanged(PlayModeStateChange state)`を`private`から`internal`へ変更し、Unity callbackとテストが同じ本体を使う。
- 新しいpublic APIは作らない。

**Produces / Consumes:**
- Produces: `ExitingPlayMode`のとき`GameShutdownEvent.FireGameShutdown(GameShutdownReason.UnawaitableExit)`を`CleanupAllSync()`より先に1回呼ぶ。
- Consumes: 既存`GameShutdownEvent`の二重発火防止、既存`CleanExitMarkWriter`の`UnawaitableExit`購読。

- [ ] **Step 1: 失敗するテストを書く** — `WebUiHostEditorCleanupTest`で一意なpid/sessionへ`CleanExitMarkWriter.InstallAtStartup`を設定し、`GameShutdownEvent.ResetForNewSession()`後に`OnPlayModeStateChanged(ExitingPlayMode)`を呼ぶ。`ConsumeSessionMarks(...).ExitedCleanly`がtrueであることを検証する。SetUp/TearDownで当該セッションの印とshutdown guardを片付ける。
- [ ] **Step 2: 対象テストが失敗することを確認** — `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value WebUiHostEditorCleanupTest`。既存実装はshutdownを発火しないためclean assertionが失敗すること。
- [ ] **Step 3: 最小実装** — `WebUiHostEditorCleanup`へ`using Client.Game.Common;`を追加し、`ExitingPlayMode`分岐で`GameShutdownEvent.FireGameShutdown(GameShutdownReason.UnawaitableExit);`を`CleanupAllSync()`より前に呼ぶ。処理意図を日英2行コメントで示す。
- [ ] **Step 4: コンパイルと対象テスト** — `uloop compile --project-path ./moorestech_client`、続けてregex `WebUiHostEditorCleanupTest|CleanExitMarkerTest|GameShutdownFlushTest`。全件PASS、Errorログ0件。
- [ ] **Step 5: 構造チェック** — 両ファイル200行未満、新規publicなし、`Func<>`/`partial`なし、Unity YAML手編集なしを確認する。
- [ ] **Step 6: コミット** — Unityが自動生成した新規`.meta`を含め、`git commit -m "fix(client): Editor停止を正常終了として記録する"`。

### Task 2: 実Editor停止の回帰確認

**Files:** なし（検証結果は報告ファイルとBeadsへ記録）

- [ ] **Step 1: 対象テストを再実行** — Task 1 Step 4のregexが全件PASSすること。
- [ ] **Step 2: PlayMode境界を確認** — 記録対象になる有人ローカル起動でPlayへ入り、Stop後に最新セッションのmarkがcleanとして消費可能で、Errorログに今回由来の例外が無いことを確認する。環境上の起動ゲートで実走不能なら、その理由を報告へ残し単体テストを代替証拠にする。
- [ ] **Step 3: コミット** — コード変更が無ければコミット不要。検証で修正した場合のみTask 1と同じゲートを再実行してコミットする。

### Task 3: 全ブランチレビュー（省略不可）

- [ ] `moores-code-review`スキルでmerge-baseからHEADまでの全ブランチレビューを実行する。
- [ ] Critical/Important所見は単一fix subagentで全件修正し、再レビューする。
- [ ] 全変更・レビュー修正・検証結果をコミットする。

### Task 4: PR作成（省略不可）

- [ ] `pr-create`スキルでpush・PR作成・masterとのコンフリクト解消まで行い、セッションを閉じられる状態にする。
