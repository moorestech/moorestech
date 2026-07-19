# WebUI Legacy Hotbar Suppression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WebUI実効モード中は旧uGUIホットバーを停止し、WebUIホットバーだけを表示する。

**Architecture:** `WebUiScreenGate` がCEFトグルとホスト稼働状態から導出した実効モードをUniRxで通知する。`HotBarView` は `GameStateController` からの表示要求を保持し、実効モード変化を表示専用の読み手として購読して最終的なGameObject表示を再評価する。

**Tech Stack:** Unity 6、C#、UniRx、NUnit、uloop、Client.Playtest録画DSL

## Global Constraints

- C#標準のAction/eventは新設せず、通知はUniRxを使う。
- `Update()`でWebUIモードをポーリングしない。
- Prefab、Scene、ScriptableObject、`.meta`を手動編集しない。
- `.cs`変更後は `uloop compile --project-path ./moorestech_client` を必ず実行する。
- テストは `--filter-type regex` で関連テストへ限定する。
- PlayModeではWebUIホットバー表示、旧uGUIホットバー停止、実プレイ画面、Errorログを確認する。
- すべての作業をコミットして終了する。

## File Structure

- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiScreenGate.cs`: WebUI実効モードの導出と変化通知を所有する。
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs`: ゲーム状態要求とWebUI実効モードを合成して旧ホットバー表示を制御する。
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiScreenGateTest.cs`: 実効モード通知のAND条件と重複通知抑止を検証する。
- `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`: `HotBarView` を独立ゲートルートとして監査対象にする。
- `docs/webui/gate-audit-2026-07-18.md`: 常駐ホットバーの実際のゲート所有者を訂正する。
- `/tmp/webui-legacy-hotbar-suppression.cs`: 録画PlayMode検証用の一時シナリオ。リポジトリへは追加しない。

## Placement Review

| 項目 | アセンブリ・層 | 機構 | 判定 |
|---|---|---|---|
| 実効モード通知 | `Client.Game` UIState基盤 | private `Subject<bool>` / public `IObservable<bool>` | 既存ゲート値そのものの通知であり、`GameStateController.OnStateChanged` と同じ配置・機構 |
| 旧ホットバー再評価 | `Client.Game` Inventory View | UniRx購読 + 明示 `SetActive`要求 | 表示専用の読み手であり、UI状態制御へ逆流しない |
| ゲート監査 | `Client.Tests` | ソース分類監査 + NUnit | 既存監査の責務内 |

データフロー: `WebUiCefToggle/WebUiHost → WebUiScreenGate → HotBarView（読み手）→ 旧uGUI GameObject`

旧ホットバーGameObjectを止める能動抑止と、論理コントローラを常時動かして描画子だけ差し替える受動統合を比較した。後者は現Prefabで描画と旧入力処理が同じGameObjectにあり、Prefab編集または旧入力との二重駆動を生む。既存のWebUI移行済みビューと同じルート抑止を採用し、Web actionが呼ぶ公開メソッドと選択状態はインスタンス参照上に維持する。

---

### Task 1: WebUI実効モードの変化通知

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiScreenGateTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiScreenGate.cs`

**Interfaces:**
- Consumes: `WebUiScreenGate.SetWebUiMode(bool)`、`WebUiScreenGate.SetHostAvailable(bool)`
- Produces: `public static IObservable<bool> OnWebUiModeChanged`

- [ ] **Step 1: 通知契約の失敗テストを書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.WebUi.Gate
{
    public class WebUiScreenGateTest
    {
        [SetUp]
        public void SetUp()
        {
            WebUiScreenGate.SetWebUiMode(false);
            WebUiScreenGate.SetHostAvailable(false);
        }

        [TearDown]
        public void TearDown()
        {
            WebUiScreenGate.SetWebUiMode(false);
            WebUiScreenGate.SetHostAvailable(false);
        }

        [Test]
        public void EffectiveModeChangesArePublishedWithoutDuplicates()
        {
            // CEFトグルとホスト稼働状態のANDが変わった場合だけ通知する
            // Publish only when the AND of the CEF toggle and host availability changes
            var changes = new List<bool>();
            using var subscription = WebUiScreenGate.OnWebUiModeChanged.Subscribe(changes.Add);

            WebUiScreenGate.SetWebUiMode(true);
            WebUiScreenGate.SetHostAvailable(true);
            WebUiScreenGate.SetHostAvailable(true);
            WebUiScreenGate.SetWebUiMode(false);

            CollectionAssert.AreEqual(new[] { true, false }, changes);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.WebUi.Gate.WebUiScreenGateTest"
```

Expected: `WebUiScreenGate.OnWebUiModeChanged` が未定義のためコンパイル失敗。

- [ ] **Step 3: 実効モード変化だけをUniRx通知する**

`WebUiScreenGate.cs`へ以下を追加し、両setterで変更前後を比較する。

```csharp
using System;
using UniRx;

private static readonly Subject<bool> _onWebUiModeChanged = new();
public static IObservable<bool> OnWebUiModeChanged => _onWebUiModeChanged;

public static void SetWebUiMode(bool active)
{
    var previous = IsWebUiMode;
    _cefToggleActive = active;
    PublishModeChange(previous);
}

public static void SetHostAvailable(bool available)
{
    var previous = IsWebUiMode;
    IsHostAvailable = available;
    PublishModeChange(previous);
}

private static void PublishModeChange(bool previous)
{
    var current = IsWebUiMode;
    if (current == previous) return;
    _onWebUiModeChanged.OnNext(current);
}
```

- [ ] **Step 4: 限定テストが成功することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.WebUi.Gate.WebUiScreenGateTest"
```

Expected: 1 test passed、0 failed。

- [ ] **Step 5: Task 1をコミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiScreenGate.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiScreenGateTest.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiScreenGateTest.cs.meta
git commit -m "feat(webui): 実効モード変化をUniRx通知"
```

### Task 2: 旧uGUIホットバーの独立ゲート化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs`
- Modify: `docs/webui/gate-audit-2026-07-18.md`

**Interfaces:**
- Consumes: `WebUiScreenGate.IsWebUiMode`、`WebUiScreenGate.OnWebUiModeChanged`
- Produces: `HotBarView.SetActive(bool)` がゲーム状態要求を保存し、WebUIモードと合成する。

- [ ] **Step 1: `HotBarView` を独立ゲートルートへ分類する**

`WebUiGateClassification.Rules` のゲートルート先頭へ追加する。

```csharp
new Rule("Client.Game/InGame/UI/Inventory/HotBarView.cs", Category.GatedRoot, "常駐ホットバーHUD"),
```

- [ ] **Step 2: 監査テストが失敗することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.WebUi.Gate.WebUiGateAuditTest.GatedRootsContainGateToken"
```

Expected: `HotBarView.cs (ゲート参照が消えている)` を含んで失敗。

- [ ] **Step 3: `HotBarView` の表示要求と実効モードを合成する**

`HotBarView.cs`へ `using UniRx;` と表示要求フィールドを追加する。

```csharp
private bool _isGameStateVisible = true;
```

`Start`の初期化末尾で、初期化完了後に購読を開始して現在状態も一度適用する。

```csharp
// WebUI実効モードの変化で旧ホットバー表示を再評価する
// Reevaluate legacy hotbar visibility whenever the effective Web UI mode changes
WebUiScreenGate.OnWebUiModeChanged
    .Subscribe(_ => ApplyVisibility())
    .AddTo(this);
ApplyVisibility();
```

`SetActive`をゲーム状態要求の保存へ変更し、共通再評価メソッドを追加する。

```csharp
public void SetActive(bool active)
{
    _isGameStateVisible = active;
    ApplyVisibility();
}

private void ApplyVisibility()
{
    gameObject.SetActive(_isGameStateVisible && !WebUiScreenGate.IsWebUiMode);
}
```

- [ ] **Step 4: 監査文書の誤記を訂正する**

`docs/webui/gate-audit-2026-07-18.md` の常駐ホットバー行を次へ変更する。

```markdown
| 常駐ホットバーHUD | HotBarView自身のゲートで抑止・Web側は常時表示 |
```

ゲートルート説明も `PlayerInventoryViewController` と `HotBarView` が独立ルートであることを明記する。

- [ ] **Step 5: ゲート監査全体が成功することを確認する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.WebUi.Gate.WebUiGateAuditTest"
```

Expected: 3 tests passed、0 failed。

- [ ] **Step 6: Task 2をコミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiGateClassification.cs \
  docs/webui/gate-audit-2026-07-18.md
git commit -m "fix(webui): 旧uGUIホットバーを実効モード中に停止"
```

### Task 3: コンパイルと関連回帰テスト

**Files:**
- Verify only

**Interfaces:**
- Consumes: Task 1、Task 2の実装
- Produces: コンパイルと関連テストの検証証跡

- [ ] **Step 1: Unityコンパイルを実行する**

Run:

```bash
uloop compile --project-path ./moorestech_client
```

Expected: compilation succeeded、errors 0。

- [ ] **Step 2: WebUIゲート関連テストをまとめて実行する**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.WebUi.Gate"
```

Expected: `WebUiScreenGateTest` 1本と `WebUiGateAuditTest` 3本がすべて成功。

- [ ] **Step 3: Unity Errorログを確認する**

Run:

```bash
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: 今回の変更に由来するErrorなし。

### Task 4: 録画PlayModeで二重表示解消を検証

**Files:**
- Create temporarily: `/tmp/webui-legacy-hotbar-suppression.cs`
- Verify: `moorestech_client/PlaytestResults/<session>/webui-legacy-hotbar-suppression/result.json`
- Verify: `moorestech_client/PlaytestResults/<session>/webui-legacy-hotbar-suppression/01-webui-hotbar-only.png`
- Verify: `moorestech_client/PlaytestResults/<session>/webui-legacy-hotbar-suppression/recording.mp4`

**Interfaces:**
- Consumes: `PlaytestRunner.Run`、`WebUiScreenGate.IsWebUiMode`、`HotBarView`
- Produces: 実プレイ画面でWebUIのみがホットバーを描画する証跡

- [ ] **Step 1: 単純な録画シナリオを書く**

```csharp
using System.Linq;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("webui-legacy-hotbar-suppression", options, async p =>
{
    await p.SetupFlatGround();
    p.Note("WebUIホットバーだけが表示される状態を検証する");
    await p.Until(() => WebUiScreenGate.IsWebUiMode, 30f, "WebUI実効モード");

    var legacyHotbars = Object.FindObjectsOfType<HotBarView>(true);
    p.Assert(legacyHotbars.Length > 0, "旧HotBarViewがシーンに存在する");
    p.Assert(legacyHotbars.All(view => !view.gameObject.activeSelf), "旧uGUIホットバーが全て停止している");
    await p.Screenshot("01-webui-hotbar-only");
});
```

- [ ] **Step 2: PlayModeを停止して録画シナリオを実行する**

Run:

```bash
uloop control-play-mode --project-path ./moorestech_client --action stop
SKILL=.claude/skills/unity-playmode-recorded-playtest
"$SKILL/scripts/run-scenario.sh" ./moorestech_client /tmp/webui-legacy-hotbar-suppression.cs
```

Expected: `Success: true`、2 asserts passed、recording.mp4が0 byteより大きい。

- [ ] **Step 3: スクリーンショットと録画を目視する**

確認事項:

- 実プレイ視点、アバター、地面、WebUI HUDが映る。
- 画面下中央にWebUIホットバーが1列だけ映る。
- 旧uGUIホットバーの重複列が映らない。
- 録画の操作オーバーレイと検証Noteが読める。

- [ ] **Step 4: PlayMode後のErrorログと自動書換えを確認する**

Run:

```bash
uloop get-logs --project-path ./moorestech_client --log-type Error
git status --short
```

Expected: 今回の変更に由来するErrorなし。`.moorestech-external-revisions.json` や `_CompileRequester.cs` の自動書換えがあれば元へ戻し、`.meta`はUnity生成分だけを取り込む。

### Task 5: 完了監査

**Files:**
- Verify all changed files

**Interfaces:**
- Consumes: 全タスクの差分と検証結果
- Produces: 未コミット変更が残らない完成状態

- [ ] **Step 1: 規約と差分を監査する**

Run:

```bash
git diff --check
git status --short
git diff HEAD~2 --stat
rg -n "partial|event Action|#region Internal" \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiScreenGate.cs \
  moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/HotBarView.cs \
  moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiScreenGateTest.cs
```

Expected: whitespace errorなし、`partial`/新規`event Action`なし、`#region Internal`はメソッド内ローカル関数用途だけ。

- [ ] **Step 2: 最終状態を確認する**

Run:

```bash
git status --short
git log -n 4 --oneline
```

Expected: ユーザー所有の `.mso/agents/` 以外に未コミット変更なし。設計、実装、テスト関連変更がすべてコミット済み。
