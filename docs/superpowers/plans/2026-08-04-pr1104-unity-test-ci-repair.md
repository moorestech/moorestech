# PR #1104 Unity Test CI Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PR #1104のUnity EditMode Testで再現している3件を修正し、必須CIを全て成功させる。

**Architecture:** BlockIconの破棄とフレーム待機をUnityの実行モード境界で分ける。起動統合テストの検証範囲は維持し、GitHub Actions側でWeb UIとCefUnityの依存を同一ジョブ内に用意する。

**Tech Stack:** Unity 6000.3.8f1、UniTask、NUnit/Unity Test Framework、GitHub Actions、GameCI

## Global Constraints

- C#変更後は`uloop compile --project-path ./moorestech_client`を実行する。
- EditModeInPlayingTestはuloop経由で実行する。
- コメントは日本語・英語2行セットとし、ファイルは200行以下を維持する。
- テストの検証範囲を狭める`IgnoreCI`や全期間`ignoreFailingMessages`は追加しない。

---

### Task 1: BlockIconのEditMode寿命管理

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Block/BlockIconImagePhotographerLifetimeTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockIconImagePhotographer.cs`

**Interfaces:**
- Consumes: `Application.isPlaying`、`Object.Destroy`、`Object.DestroyImmediate`
- Produces: `TakeIconImages`がEditModeとPlayModeの両方で全対象を順次撮影して完了する契約

- [ ] **Step 1: REDテストを明示する**

  `LogAssert.Expect`を削除し、現在の実装が`Destroy may not be called from edit mode`で失敗すること、既存CI結果でタスクが180秒停止することを確認する。

- [ ] **Step 2: 完了待ちを有限化する**

  テスト内のPendingループへフレーム上限を設け、上限到達時に撮影タスク未完了を明示して失敗させる。

- [ ] **Step 3: 最小実装を行う**

  EditModeでは対象・RenderTexture・Cameraを`DestroyImmediate`し、PlayModeでのみ`Destroy`とループ末尾の`UniTask.Yield(PlayerLoopTiming.Update)`を実行する。

- [ ] **Step 4: 対象テストとcompileを実行する**

  Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\\.Tests\\.Block\\.BlockIconImagePhotographerLifetimeTest"`

  Run: `uloop compile --project-path ./moorestech_client`

  Expected: 対象2件が成功し、Errorログが残らない。

- [ ] **Step 5: BlockIcon修正をコミットする**

  Commit message: `fix: EditModeのアイコン撮影資源を同期破棄する`

### Task 2: Unity Testジョブの起動依存

**Files:**
- Modify: `.github/workflows/run_test.yml`
- Reference: `.github/workflows/build.yml`

**Interfaces:**
- Consumes: `moorestech_web/setup.sh`、`moorestech_web/webui/pnpm-lock.yaml`
- Produces: Unity TestコンテナからNode/pnpmとwebui node_modulesが利用でき、Linux Editorでは非互換なCEF表示層を起動しないジョブ環境

- [ ] **Step 1: REDの環境境界を確認する**

  既存Attempt 1/2の`Node binary not found`と`cef_unity_rust`ログ、およびUnity Testジョブに準備ステップが無いことを記録する。

- [ ] **Step 2: Linux EditorのCEF表示層を無効化する**

  上流LinuxバイナリはGameCIのglibcとABI互換がないため、`WebUiCefNavigator.Awake`でLinux EditorのCEF表示コンポーネントだけを無効化する。

- [ ] **Step 3: Web UIツールチェーンを準備する**

  `moorestech_web/setup.sh`を実行し、検出したpnpmで`webui install --frozen-lockfile`を実行する。

- [ ] **Step 4: workflow構文を検証する**

  YAMLを読み直し、既存Web UI TestとUnity Testのworking-directory、相対パス、実行順を照合する。

- [ ] **Step 5: CI環境修正をコミットする**

  Commit message: `ci: Unity TestのWeb UI起動依存を準備する`

### Task 3: Push後のCI収束

**Files:**
- Verify: PR #1104 GitHub Actions checks

**Interfaces:**
- Consumes: PRブランチ`feat/map-autogen-p3`
- Produces: 全必須チェックがSUCCESSのPR

- [ ] **Step 1: 差分と機械チェックを確認する**

  `git diff --check`、関連テスト、Unity compile、リポジトリ規約チェックを実行する。

- [ ] **Step 2: PRブランチへpushする**

  `git push origin feat/map-autogen-p3`を実行する。

- [ ] **Step 3: GitHub Actionsを監視する**

  `gh pr checks 1104 --repo moorestech/moorestech --watch`と失敗ジョブログを使い、全必須チェックが成功するまで原因修正を反復する。

- [ ] **Step 4: Beadsをcloseする**

  全必須CI成功後に`bd close moorestech-0bp --reason="PR #1104 required checks are green"`を実行する。
