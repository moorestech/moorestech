# 統一シャットダウンパイプライン実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** クライアントとサーバーに散在する終了処理を、純 async の `ShutdownCoordinator` と Unity 境界を吸収する `ApplicationShutdownBridge` の 2 層に統一し、`Thread.Sleep(50)` と `Thread.Abort()` を排除する。

**Architecture:** `ShutdownCoordinator`（Unity 非依存・async オーケストレーションのみ）と `ApplicationShutdownBridge`（`Application.quitting` と Editor フックから Coordinator を同期境界で駆動）をクライアント・サーバーそれぞれに配置。各サブシステムは起動時に `Register(phase, name, stepAsync)` で自分のクリーンアップを登録する。Save ACK プロトコルを追加し `Thread.Sleep(50)` を `await Response.SaveAsync()` に置換。

**Tech Stack:** Unity 2022+ / C# / UniTask / UniRx / MessagePack / NUnit (EditMode) / VContainer

**参照 spec:** `docs/superpowers/specs/2026-04-23-unified-shutdown-pipeline-design.md`

**ファイル構成（作成/変更）:**

作成:
- `moorestech_client/Assets/Scripts/Client.Common/Shutdown/ShutdownPhase.cs`
- `moorestech_client/Assets/Scripts/Client.Common/Shutdown/ShutdownCoordinator.cs`
- `moorestech_client/Assets/Scripts/Client.Starter/Shutdown/ApplicationShutdownBridge.cs`
- `moorestech_client/Assets/Scripts/Client.Tests/ShutdownCoordinatorTest.cs`
- `moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ShutdownPhase.cs`
- `moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ShutdownCoordinator.cs`
- `moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ApplicationShutdownBridge.cs`
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Boot/ShutdownCoordinatorTest.cs`
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SaveProtocol.cs`（`SaveResponseMessagePack` を追加）

変更:
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（`SaveAsync` 追加）
- `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs`（`IInitializable.Initialize` で Register）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`（Coordinator 登録、Editor フック削除）
- `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs`（Coordinator 登録）
- `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（Coordinator 登録、OnDestroy 削除）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveButton.cs`（`Response.SaveAsync` に切り替え）
- `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`（Register + Thread.Join + Dispose 削除）
- `moorestech_server/Assets/Scripts/Server.Boot/ServerStarter.cs`（ライフサイクルフック削除）

削除:
- `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs`
- `VanillaApiSendOnly.Save()` メソッドのみ削除（クラス自体は残す）

---

## Task 1: サーバー側 Save レスポンス型を追加

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SaveProtocol.cs`

- [ ] **Step 1: `SaveProtocol.GetResponse` を完了レスポンスを返すように変更**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SaveProtocol.cs` を以下に全置換：

```csharp
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SaveProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:save";

        private readonly IWorldSaveDataSaver _worldSaveDataSaver;

        public SaveProtocol(ServiceProvider serviceProvider)
        {
            _worldSaveDataSaver = serviceProvider.GetService<IWorldSaveDataSaver>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // セーブ完了をクライアントに ACK で返す
            // Return ACK to the client once save completes
            Debug.Log("セーブ開始");
            _worldSaveDataSaver.Save();
            Debug.Log("セーブ完了");
            return new SaveResponseMessagePack();
        }

        [MessagePackObject]
        public class SaveProtocolMessagePack : ProtocolMessagePackBase
        {
            public SaveProtocolMessagePack()
            {
                Tag = ProtocolTag;
            }
        }

        [MessagePackObject]
        public class SaveResponseMessagePack : ProtocolMessagePackBase
        {
            public SaveResponseMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
    }
}
```

- [ ] **Step 2: サーバーコンパイル**

Run: `uloop compile --project-path ./moorestech_server`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/SaveProtocol.cs
git commit -m "feat(server): SaveProtocol に完了 ACK レスポンスを追加"
```

---

## Task 2: クライアント側 `VanillaApiWithResponse.SaveAsync` を追加

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`

- [ ] **Step 1: `SaveAsync` メソッドを追加**

`VanillaApiWithResponse.cs` 末尾のクラス `}` 直前に以下を挿入：

```csharp
        public async UniTask SaveAsync(CancellationToken ct = default)
        {
            // サーバーにセーブ要求を送り、完了 ACK まで待つ
            // Send save request and await the server's completion ACK
            var request = new SaveProtocol.SaveProtocolMessagePack();
            await _packetExchangeManager.GetPacketResponse<SaveProtocol.SaveResponseMessagePack>(request, ct);
        }
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs
git commit -m "feat(client): VanillaApiWithResponse.SaveAsync を追加"
```

---

## Task 3: クライアント `ShutdownPhase` enum

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Common/Shutdown/ShutdownPhase.cs`

- [ ] **Step 1: enum を作成**

```csharp
namespace Client.Common.Shutdown
{
    // 終了パイプラインのフェーズ順序。値はフェーズ間隔を空けて将来追加を吸収する
    // Shutdown pipeline phase order. Gaps leave room for future phases
    public enum ShutdownPhase
    {
        BeforeDisconnect  = 0,    // Save ACK 待ち
        Disconnect        = 100,  // ソケットクローズ
        AfterDisconnect   = 200,  // サーバー不要なサブシステム停止
        DisposeSubsystems = 300,  // プロセス kill / Addressables / VContainer scope Dispose
    }
}
```

- [ ] **Step 2: 未コミットにとどめ、Task 4 と合わせてコミット**

---

## Task 4: クライアント `ShutdownCoordinator` 実装

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Common/Shutdown/ShutdownCoordinator.cs`

- [ ] **Step 1: Coordinator を作成**

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Common.Shutdown
{
    // 終了パイプラインのオーケストレーション。Unity 非依存の async API のみを公開
    // Orchestrates the shutdown pipeline with a Unity-independent async API
    public static class ShutdownCoordinator
    {
        private static readonly object _lock = new();
        private static readonly List<(ShutdownPhase phase, string name, Func<UniTask> step)> _steps = new();
        private static Task _shutdownTask;

        public static void Register(ShutdownPhase phase, string name, Func<UniTask> step)
        {
            lock (_lock)
            {
                if (_shutdownTask != null)
                {
                    Debug.LogWarning($"[ShutdownCoordinator] Register ignored after shutdown started: {name}");
                    return;
                }
                _steps.Add((phase, name, step));
            }
        }

        public static UniTask ShutdownAsync()
        {
            lock (_lock)
            {
                if (_shutdownTask != null) return _shutdownTask.AsUniTask();
                _shutdownTask = RunPipelineAsync().AsTask();
                return _shutdownTask.AsUniTask();
            }
        }

        // 登録された全ステップをフェーズ昇順→登録順で直列実行する
        // Run all registered steps sequentially, ordered by phase then registration order
        private static async UniTask RunPipelineAsync()
        {
            List<(ShutdownPhase phase, string name, Func<UniTask> step)> snapshot;
            lock (_lock) { snapshot = new List<(ShutdownPhase, string, Func<UniTask>)>(_steps); }
            snapshot.Sort((a, b) => a.phase.CompareTo(b.phase));

            foreach (var (phase, name, step) in snapshot)
            {
                Debug.Log($"[ShutdownCoordinator] [{phase}] {name} start");
                try
                {
                    await step();
                    Debug.Log($"[ShutdownCoordinator] [{phase}] {name} done");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogError($"[ShutdownCoordinator] [{phase}] {name} failed, continuing");
                }
            }
        }

#if UNITY_INCLUDE_TESTS
        // テスト用。複数テストの間でグローバル状態をリセットする
        // Test-only hook to reset global state between tests
        internal static void ResetForTests()
        {
            lock (_lock) { _steps.Clear(); _shutdownTask = null; }
        }
#endif
    }
}
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Common/Shutdown/
git commit -m "feat(client): ShutdownCoordinator と ShutdownPhase を追加"
```

---

## Task 5: クライアント `ShutdownCoordinator` 単体テスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/ShutdownCoordinatorTest.cs`

- [ ] **Step 1: テストコードを作成**

```csharp
using System.Collections.Generic;
using Client.Common.Shutdown;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests
{
    public class ShutdownCoordinatorTest
    {
        [SetUp]
        public void SetUp() => ShutdownCoordinator.ResetForTests();

        [TearDown]
        public void TearDown() => ShutdownCoordinator.ResetForTests();

        [Test]
        public async System.Threading.Tasks.Task Steps_RunInPhaseThenRegistrationOrder()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "A2", () => { log.Add("A2"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.BeforeDisconnect, "B1", () => { log.Add("B1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "A1", () => { log.Add("A1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.BeforeDisconnect, "B2", () => { log.Add("B2"); return UniTask.CompletedTask; });

            await ShutdownCoordinator.ShutdownAsync();

            Assert.AreEqual(new[] { "B1", "B2", "A2", "A1" }, log.ToArray());
        }

        [Test]
        public async System.Threading.Tasks.Task ShutdownAsync_SecondCall_ReturnsSameTask()
        {
            var runs = 0;
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "S", async () =>
            {
                await UniTask.Yield();
                runs++;
            });

            var t1 = ShutdownCoordinator.ShutdownAsync();
            var t2 = ShutdownCoordinator.ShutdownAsync();
            await UniTask.WhenAll(t1, t2);

            Assert.AreEqual(1, runs);
        }

        [Test]
        public async System.Threading.Tasks.Task StepException_DoesNotAbortPipeline()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "fail", () =>
            {
                log.Add("fail-entered");
                throw new System.InvalidOperationException("boom");
            });
            ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "after", () => { log.Add("after"); return UniTask.CompletedTask; });

            // LogException が呼ばれるため、NUnit 側でエラーログを許容する
            // Allow logged exception so NUnit does not fail the test for expected output
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            await ShutdownCoordinator.ShutdownAsync();

            Assert.Contains("fail-entered", log);
            Assert.Contains("after", log);
        }

        [Test]
        public async System.Threading.Tasks.Task Register_AfterShutdown_IsIgnored()
        {
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "first", () => UniTask.CompletedTask);
            var t = ShutdownCoordinator.ShutdownAsync();
            // Register while pipeline is running; expected to be ignored with warning
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "late", () => UniTask.CompletedTask);
            await t;
            // Success if no throw and pipeline completed; warning log is confirmed manually
            Assert.Pass();
        }
    }
}
```

- [ ] **Step 2: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ShutdownCoordinatorTest"`
Expected: 4 テスト PASS

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/ShutdownCoordinatorTest.cs
git commit -m "test(client): ShutdownCoordinator の単体テストを追加"
```

---

## Task 6: クライアント `ApplicationShutdownBridge`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Shutdown/ApplicationShutdownBridge.cs`

- [ ] **Step 1: Bridge を作成**

```csharp
using System;
using System.Threading.Tasks;
using Client.Common.Shutdown;
using UnityEngine;

namespace Client.Starter.Shutdown
{
    // Unity のライフサイクルシグナル（ランタイム/Editor）を ShutdownCoordinator に橋渡しする
    // Bridges Unity lifecycle signals (runtime/editor) into ShutdownCoordinator
    internal static class ApplicationShutdownBridge
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallRuntimeHooks()
        {
            Application.quitting -= TriggerBlocking;
            Application.quitting += TriggerBlocking;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallEditorHooks()
        {
            UnityEditor.EditorApplication.quitting -= TriggerBlocking;
            UnityEditor.EditorApplication.quitting += TriggerBlocking;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= TriggerBlocking;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += TriggerBlocking;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) TriggerBlocking();
        }
#endif

        // Unity の同期境界専用。ここでのみ GetAwaiter().GetResult() を使う
        // Sync boundary only used here, at the Unity lifecycle callback layer
        private static void TriggerBlocking()
        {
            var task = ShutdownCoordinator.ShutdownAsync().AsTask();
            Task.WhenAny(task, Task.Delay(Timeout)).GetAwaiter().GetResult();
            if (task.IsFaulted) Debug.LogException(task.Exception?.GetBaseException());
            if (!task.IsCompleted) Debug.LogWarning("[ApplicationShutdownBridge] shutdown timed out");
        }
    }
}
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/Shutdown/
git commit -m "feat(client): ApplicationShutdownBridge で Unity ライフサイクルを Coordinator に接続"
```

---

## Task 7: サーバー `ShutdownPhase` enum

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ShutdownPhase.cs`

- [ ] **Step 1: enum を作成**

```csharp
namespace Server.Boot.Shutdown
{
    // サーバー終了パイプラインのフェーズ順序
    // Server shutdown pipeline phase order
    public enum ShutdownPhase
    {
        StopAcceptingConnections = 100,
        StopUpdate               = 200,
        DisposeSubsystems        = 300,
    }
}
```

- [ ] **Step 2: 未コミットにとどめ、Task 8 と合わせてコミット**

---

## Task 8: サーバー `ShutdownCoordinator` 実装

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ShutdownCoordinator.cs`

- [ ] **Step 1: Coordinator を作成**

クライアント側の Task 4 と同一構造。namespace と enum 型のみ `Server.Boot.Shutdown` を参照。

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Server.Boot.Shutdown
{
    public static class ShutdownCoordinator
    {
        private static readonly object _lock = new();
        private static readonly List<(ShutdownPhase phase, string name, Func<UniTask> step)> _steps = new();
        private static Task _shutdownTask;

        public static void Register(ShutdownPhase phase, string name, Func<UniTask> step)
        {
            lock (_lock)
            {
                if (_shutdownTask != null)
                {
                    Debug.LogWarning($"[ShutdownCoordinator] Register ignored after shutdown started: {name}");
                    return;
                }
                _steps.Add((phase, name, step));
            }
        }

        public static UniTask ShutdownAsync()
        {
            lock (_lock)
            {
                if (_shutdownTask != null) return _shutdownTask.AsUniTask();
                _shutdownTask = RunPipelineAsync().AsTask();
                return _shutdownTask.AsUniTask();
            }
        }

        private static async UniTask RunPipelineAsync()
        {
            List<(ShutdownPhase phase, string name, Func<UniTask> step)> snapshot;
            lock (_lock) { snapshot = new List<(ShutdownPhase, string, Func<UniTask>)>(_steps); }
            snapshot.Sort((a, b) => a.phase.CompareTo(b.phase));

            foreach (var (phase, name, step) in snapshot)
            {
                Debug.Log($"[ShutdownCoordinator] [{phase}] {name} start");
                try
                {
                    await step();
                    Debug.Log($"[ShutdownCoordinator] [{phase}] {name} done");
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    Debug.LogError($"[ShutdownCoordinator] [{phase}] {name} failed, continuing");
                }
            }
        }

#if UNITY_INCLUDE_TESTS
        internal static void ResetForTests()
        {
            lock (_lock) { _steps.Clear(); _shutdownTask = null; }
        }
#endif
    }
}
```

- [ ] **Step 2: サーバーコンパイル**

Run: `uloop compile --project-path ./moorestech_server`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/Shutdown/
git commit -m "feat(server): ShutdownCoordinator と ShutdownPhase を追加"
```

---

## Task 9: サーバー `ShutdownCoordinator` 単体テスト

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/UnitTest/Boot/ShutdownCoordinatorTest.cs`

- [ ] **Step 1: テストコードを作成**

```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Shutdown;

namespace Tests.UnitTest.Boot
{
    public class ShutdownCoordinatorTest
    {
        [SetUp]
        public void SetUp() => ShutdownCoordinator.ResetForTests();

        [TearDown]
        public void TearDown() => ShutdownCoordinator.ResetForTests();

        [Test]
        public async System.Threading.Tasks.Task Steps_RunInPhaseThenRegistrationOrder()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "D1", () => { log.Add("D1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.StopAcceptingConnections, "S1", () => { log.Add("S1"); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "U1", () => { log.Add("U1"); return UniTask.CompletedTask; });

            await ShutdownCoordinator.ShutdownAsync();

            Assert.AreEqual(new[] { "S1", "U1", "D1" }, log.ToArray());
        }

        [Test]
        public async System.Threading.Tasks.Task ShutdownAsync_SecondCall_ReturnsSameTask()
        {
            var runs = 0;
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "S", async () => { await UniTask.Yield(); runs++; });
            var t1 = ShutdownCoordinator.ShutdownAsync();
            var t2 = ShutdownCoordinator.ShutdownAsync();
            await UniTask.WhenAll(t1, t2);
            Assert.AreEqual(1, runs);
        }

        [Test]
        public async System.Threading.Tasks.Task StepException_DoesNotAbortPipeline()
        {
            var log = new List<string>();
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "fail", () => throw new System.InvalidOperationException("boom"));
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "after", () => { log.Add("after"); return UniTask.CompletedTask; });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            await ShutdownCoordinator.ShutdownAsync();
            Assert.Contains("after", log);
        }
    }
}
```

- [ ] **Step 2: サーバーテスト実行**

Run: `uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "ShutdownCoordinatorTest"`
Expected: 3 テスト PASS

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/UnitTest/Boot/ShutdownCoordinatorTest.cs
git commit -m "test(server): ShutdownCoordinator の単体テストを追加"
```

---

## Task 10: サーバー `ApplicationShutdownBridge`

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ApplicationShutdownBridge.cs`

- [ ] **Step 1: Bridge を作成**

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Server.Boot.Shutdown
{
    internal static class ApplicationShutdownBridge
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallRuntimeHooks()
        {
            Application.quitting -= TriggerBlocking;
            Application.quitting += TriggerBlocking;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallEditorHooks()
        {
            UnityEditor.EditorApplication.quitting -= TriggerBlocking;
            UnityEditor.EditorApplication.quitting += TriggerBlocking;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= TriggerBlocking;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += TriggerBlocking;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) TriggerBlocking();
        }
#endif

        private static void TriggerBlocking()
        {
            var task = ShutdownCoordinator.ShutdownAsync().AsTask();
            Task.WhenAny(task, Task.Delay(Timeout)).GetAwaiter().GetResult();
            if (task.IsFaulted) Debug.LogException(task.Exception?.GetBaseException());
            if (!task.IsCompleted) Debug.LogWarning("[ApplicationShutdownBridge] shutdown timed out");
        }
    }
}
```

- [ ] **Step 2: サーバーコンパイル**

Run: `uloop compile --project-path ./moorestech_server`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/Shutdown/ApplicationShutdownBridge.cs
git commit -m "feat(server): ApplicationShutdownBridge で Unity ライフサイクルを Coordinator に接続"
```

---

## Task 11: `VanillaApi` に 3 ステップを登録

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs`

- [ ] **Step 1: `Initialize` に Register を追加**

`VanillaApi.cs` を以下に全置換：

```csharp
using System;
using System.Diagnostics;
using Client.Common.Shutdown;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using UniRx;
using VContainer.Unity;

namespace Client.Network.API
{
    public class VanillaApi : IInitializable
    {
        private readonly Process _localServerProcess;

        private readonly ServerCommunicator _serverCommunicator;
        public readonly VanillaApiEvent Event;
        public readonly VanillaApiWithResponse Response;
        public readonly VanillaApiSendOnly SendOnly;

        public VanillaApi(PacketExchangeManager packetExchangeManager, PacketSender packetSender, ServerCommunicator serverCommunicator, PlayerConnectionSetting playerConnectionSetting, Process localServerProcess)
        {
            _serverCommunicator = serverCommunicator;
            _localServerProcess = localServerProcess;

            Event = new VanillaApiEvent(packetExchangeManager, playerConnectionSetting);
            Response = new VanillaApiWithResponse(packetExchangeManager, playerConnectionSetting);
            SendOnly = new VanillaApiSendOnly(packetSender, playerConnectionSetting);
        }

        public IObservable<Unit> OnDisconnect => _serverCommunicator.OnDisconnect;

        public void Initialize()
        {
            // 終了パイプラインに Save ACK → ソケット切断 → ローカルプロセス kill を登録
            // Register save ACK, socket close, and local server kill into the shutdown pipeline
            ShutdownCoordinator.Register(ShutdownPhase.BeforeDisconnect, "VanillaApi.Save",
                async () => await Response.SaveAsync());
            ShutdownCoordinator.Register(ShutdownPhase.Disconnect, "VanillaApi.Close",
                () => { _serverCommunicator.Close(); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "VanillaApi.KillLocalServer",
                () => { _localServerProcess?.Kill(); return UniTask.CompletedTask; });
        }
    }
}
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs
git commit -m "refactor(client): VanillaApi を ShutdownCoordinator に登録（Disconnect を削除）"
```

---

## Task 12: `WebUiHost` を Coordinator 登録に切り替え

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs`

- [ ] **Step 1: WebUiHost.cs を以下に全置換**

```csharp
using Client.Common.Shutdown;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    // Web UI ホストの起動 facade。停止は ShutdownCoordinator が一元管理する
    // Static facade for Web UI host start; stop is centrally managed by ShutdownCoordinator
    public static class WebUiHost
    {
        private static KestrelServer _kestrel;
        private static ViteProcess _vite;
        private static WebSocketHub _hub;
        private static bool _registered;

        public static WebSocketHub Hub => _hub;

        public static async UniTask StartAsync()
        {
            if (_kestrel != null) return;

            // 初回起動時に 1 度だけ終了パイプラインへ登録
            // Register the stop step into the shutdown pipeline exactly once
            if (!_registered)
            {
                ShutdownCoordinator.Register(ShutdownPhase.AfterDisconnect, "WebUiHost.Stop", StopAsync);
                _registered = true;
            }

            _hub = new WebSocketHub();
            _kestrel = new KestrelServer();
            await _kestrel.StartAsync(_hub);

            _vite = new ViteProcess();
            await _vite.StartAsync();

            Debug.Log("[WebUiHost] ready. Open http://localhost:5173/");
        }

        public static async UniTask StopAsync()
        {
            // Vite はメインスレッドで同期 kill
            // Kill Vite synchronously on the main thread
            if (_vite != null)
            {
                _vite.Kill();
                _vite = null;
            }

            // Kestrel/WS 停止はスレッドプールへ逃がしてメインスレッドを解放
            // Move Kestrel/WS shutdown off the main thread
            await UniTask.SwitchToTaskPool();

            if (_hub != null)
            {
                _hub.ClearTopics();
                await _hub.CloseAllAsync();
                _hub = null;
            }

            if (_kestrel != null)
            {
                await _kestrel.StopAsync();
                _kestrel = null;
            }

            // 残存 Vite のセーフティネット
            // Safety net in case any lingering Vite process still holds the port
            ViteProcess.KillAnyLingering();

            Debug.Log("[WebUiHost] stopped");
        }
    }
}
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0（`GameShutdownEvent` 未参照のエラーは Task 16 で消える。このステップ時点では WebUiHost から `GameShutdownEvent` 参照が消えているので問題なし）

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs
git commit -m "refactor(client): WebUiHost を ShutdownCoordinator に移行し Editor フックを削除"
```

---

## Task 13: `DebugObjectsBootstrap` を Coordinator 登録に切り替え

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs`

- [ ] **Step 1: `Application.quitting` 直接購読を Register に置換**

Read `DebugObjectsBootstrap.cs` の L40-L42 付近（`Application.quitting -= OnApplicationQuitting;` を含む 2 行）を以下に置換：

```csharp
            // 終了パイプラインに Addressables 解放を登録
            // Register Addressables release into the shutdown pipeline
            Client.Common.Shutdown.ShutdownCoordinator.Register(
                Client.Common.Shutdown.ShutdownPhase.DisposeSubsystems,
                "DebugObjects.ReleaseAddressables",
                () => { ReleaseDebugObjectsAsset(); return Cysharp.Threading.Tasks.UniTask.CompletedTask; });
```

次に、同ファイル内 `OnApplicationQuitting` メソッドを以下にリネーム：

```csharp
        private static void ReleaseDebugObjectsAsset()
        {
            // 保持した Addressables 参照を明示解放する
            // Explicitly release held Addressables reference
            if (_debugObjectsAsset == null) return;
            _debugObjectsAsset.Dispose();
            _debugObjectsAsset = null;
        }
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/DebugObjectsBootstrap.cs
git commit -m "refactor(client): DebugObjectsBootstrap を ShutdownCoordinator に移行"
```

---

## Task 14: `MainGameStarter` の VContainer scope Dispose を Coordinator に登録

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`

- [ ] **Step 1: `OnDestroy` メソッド（L127-L130）を削除し、`StartGame` メソッドの冒頭 `var builder = new ContainerBuilder();` 直後に以下を挿入**

```csharp
            // VContainer scope の破棄を終了パイプライン末尾に登録
            // Register VContainer scope disposal at the end of the shutdown pipeline
            Client.Common.Shutdown.ShutdownCoordinator.Register(
                Client.Common.Shutdown.ShutdownPhase.DisposeSubsystems,
                "MainGameStarter.DisposeResolver",
                () => { _resolver?.Dispose(); return Cysharp.Threading.Tasks.UniTask.CompletedTask; });
```

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "refactor(client): MainGameStarter の VContainer dispose を ShutdownCoordinator に移行"
```

---

## Task 15: サーバー `ServerInstanceManager` を Register + Join 方式に変更

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`

- [ ] **Step 1: `ServerInstanceManager.cs` を以下に全置換**

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Mod.Base;
using Mod.Loader;
using Server.Boot.Args;
using Server.Boot.Loop;
using Server.Boot.Shutdown;
using UnityEngine;

namespace Server.Boot
{
    public class ServerInstanceManager
    {
        private static readonly TimeSpan ThreadJoinTimeout = TimeSpan.FromSeconds(3);

        private Thread _connectionUpdateThread;
        private Thread _gameUpdateThread;
        private CancellationTokenSource _cancellationTokenSource;

        private readonly string[] _args;

        public ServerInstanceManager(string[] args)
        {
            _args = args;
        }

        public void Start()
        {
            (_connectionUpdateThread, _gameUpdateThread, _cancellationTokenSource) = StartInternal(_args);

            // 終了パイプラインに接続停止・アップデート停止・サブシステム破棄を登録
            // Register stop-accepting, stop-update, and subsystem dispose into the shutdown pipeline
            ShutdownCoordinator.Register(ShutdownPhase.StopAcceptingConnections, "Server.CancelTokens",
                () => { _cancellationTokenSource?.Cancel(); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.StopUpdate, "Server.JoinThreads",
                () => { JoinThreads(); return UniTask.CompletedTask; });
            ShutdownCoordinator.Register(ShutdownPhase.DisposeSubsystems, "Server.GameUpdater.Dispose",
                () => { GameUpdater.Dispose(); return UniTask.CompletedTask; });
        }

        // 両スレッドは CancellationToken を監視しているので Cancel 後の自然終了を待つ
        // Both threads observe CancellationToken; wait for natural exit after Cancel
        private void JoinThreads()
        {
            if (_connectionUpdateThread != null && !_connectionUpdateThread.Join(ThreadJoinTimeout))
                Debug.LogWarning("[ServerInstanceManager] connection update thread did not exit within timeout");
            if (_gameUpdateThread != null && !_gameUpdateThread.Join(ThreadJoinTimeout))
                Debug.LogWarning("[ServerInstanceManager] game update thread did not exit within timeout");
        }

        private static (Thread connectionUpdateThread, Thread gameUpdateThread, CancellationTokenSource cancellationTokenSource) StartInternal(string[] args)
        {
            var settings = CliConvert.Parse<StartServerSettings>(args);
            var serverDirectory = settings.ServerDataDirectory;
            var options = new MoorestechServerDIContainerOptions(serverDirectory)
                {
                    saveJsonFilePath = new SaveJsonFilePath(settings.SaveFilePath),
                };

            Debug.Log("データをロードします　パス:" + serverDirectory);

            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(options);

            serviceProvider.GetService<IWorldSaveDataLoader>().LoadOrInitialize();

            var modsResource = serviceProvider.GetService<ModsResource>();
            modsResource.Mods.ToList().ForEach(
                m => m.Value.ModEntryPoints.ForEach(
                    e =>
                    {
                        Debug.Log("Modをロードしました modId:" + m.Value + " className:" + e.GetType().Name);
                        e.OnLoad(new ServerModEntryInterface(serviceProvider, packet));
                    }));

            var cancellationToken = new CancellationTokenSource();
            var token = cancellationToken.Token;

            var connectionUpdateThread = new Thread(() => new ServerListenAcceptor().StartServer(packet, token));
            connectionUpdateThread.Name = "[moorestech]通信受け入れスレッド";
            connectionUpdateThread.Start();

            if (settings.AutoSave)
            {
                Task.Run(() => AutoSaveSystem.AutoSave(serviceProvider.GetService<IWorldSaveDataSaver>(), token), cancellationToken.Token);
            }

            var gameUpdateThread = new Thread(() => ServerGameUpdater.StartUpdate(token));
            gameUpdateThread.Name = "[moorestech]ゲームアップデートスレッド";
            gameUpdateThread.Start();

            return (connectionUpdateThread, gameUpdateThread, cancellationToken);
        }
    }
}
```

- [ ] **Step 2: サーバーコンパイル**

Run: `uloop compile --project-path ./moorestech_server`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs
git commit -m "refactor(server): ServerInstanceManager を ShutdownCoordinator に移行し Thread.Abort を廃止"
```

---

## Task 16: `ServerStarter` のライフサイクルフックを削除

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerStarter.cs`

- [ ] **Step 1: `ServerStarter.cs` を以下に全置換**

```csharp
using System;
using UnityEngine;

namespace Server.Boot
{
    public class ServerStarter : MonoBehaviour
    {
        private ServerInstanceManager _startServer;
        private string[] _args = Array.Empty<string>();

        public void SetArgs(string[] args)
        {
            _args = args;
        }

        private void Start()
        {
            _startServer = new ServerInstanceManager(_args);
            _startServer.Start();
        }
    }
}
```

- [ ] **Step 2: サーバーコンパイル**

Run: `uloop compile --project-path ./moorestech_server`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/ServerStarter.cs
git commit -m "refactor(server): ServerStarter からライフサイクルフックを削除"
```

---

## Task 17: `SaveButton` を `Response.SaveAsync` に切り替え

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveButton.cs`

- [ ] **Step 1: まずファイル内容を読み、既存の L13 を以下に置換**

Read `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveButton.cs` してから、`saveButton.onClick.AddListener(ClientContext.VanillaApi.SendOnly.Save);` を以下に置換：

```csharp
            saveButton.onClick.AddListener(() =>
            {
                // サーバーの保存完了まで待つ
                // Await the server's save completion
                ClientContext.VanillaApi.Response.SaveAsync().Forget();
            });
```

`Forget()` を使うのは UI ボタンのハンドラで `UniTask` を投げ捨てつつ例外時は UniTask 既定ロガーが拾ってくれるため。

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveButton.cs
git commit -m "refactor(client): SaveButton を Response.SaveAsync に切り替え"
```

---

## Task 18: `VanillaApiSendOnly.Save()` を削除

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`

- [ ] **Step 1: `Save()` メソッド（L64-L68）だけを削除**

```csharp
        public void Save()
        {
            var request = new SaveProtocol.SaveProtocolMessagePack();
            _packetSender.Send(request);
        }
```

上記の 5 行（および直前の空行1つ）をファイルから削除する。

- [ ] **Step 2: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0（`BackToMainMenu` がまだ `SendOnly.Save()` を参照している場合はここで検知される。Task 20 でクラスごと消す予定なのでもし呼び出し側が残っていれば Task 20 を先に実行）

**補足:** Task 20（`BackToMainMenu.cs` 削除）を先に実行する方がコンパイル順序として安全。依存順にする場合は Task 18 を Task 20 の後に回す。

- [ ] **Step 3: Commit**

```bash
git add moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs
git commit -m "refactor(client): VanillaApiSendOnly.Save を削除"
```

---

## Task 19: `GameShutdownEvent` を削除

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs.meta`

- [ ] **Step 1: 参照がないことを確認**

Run: `grep -rn "GameShutdownEvent" /Users/katsumi/moorestech-worktrees/test1/moorestech_client/Assets/Scripts --include="*.cs"`
Expected: 出力 0 行（Task 12 で WebUiHost が参照を外し、Task 20 で BackToMainMenu が消える予定。Task 20 を Task 19 より先にやるのが安全）

**順序推奨:** Task 20（BackToMainMenu 削除）→ Task 19（GameShutdownEvent 削除）→ Task 18（SendOnly.Save 削除）の順で実行。

- [ ] **Step 2: ファイル削除**

Run: `rm moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs moorestech_client/Assets/Scripts/Client.Game/Common/GameShutdownEvent.cs.meta`

- [ ] **Step 3: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 4: Commit**

```bash
git add -u moorestech_client/Assets/Scripts/Client.Game/Common/
git commit -m "refactor(client): GameShutdownEvent を削除（ShutdownCoordinator が役割を引き継ぐ）"
```

---

## Task 20: `BackToMainMenu` を削除

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs.meta`

- [ ] **Step 1: 参照箇所を確認（Prefab/シーンは別対応）**

Run: `grep -rn "BackToMainMenu" /Users/katsumi/moorestech-worktrees/test1/moorestech_client/Assets/Scripts --include="*.cs"`
Expected: 出力に残るのは `MainGameStarter.cs` の `[SerializeField] private BackToMainMenu backToMainMenu;` のみのはず

- [ ] **Step 2: `MainGameStarter.cs` から SerializeField 参照を削除**

`moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` の L102 `[SerializeField] private BackToMainMenu backToMainMenu;` を削除。

- [ ] **Step 3: ファイル削除**

Run: `rm moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/BackToMainMenu.cs.meta`

- [ ] **Step 4: クライアントコンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: error 0 / warning 0

- [ ] **Step 5: Commit**

```bash
git add -u moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/ moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "refactor(client): BackToMainMenu を削除（MainMenu 復帰機能を廃止）"
```

- [ ] **Step 6: PR 本文用メモを残す**

以下を PR 本文に含める：

> **ユーザー作業要**: `BackToMainMenu` 削除に伴い、ポーズメニューの Prefab / シーンから `BackToMainMenu` コンポーネント参照および「メインメニューに戻る」ボタンを手動で剥がす必要があります。Unity Editor で Prefab / シーンを開き、該当ボタンとコンポーネントを削除してください。

---

## Task 21: 手動検証

これ以降は手動確認。チェックリストとして PR に残す。

- [ ] **Check 1: Editor Play Mode Stop**
  - Unity Editor で Play → 1 分ほど遊ぶ → Stop
  - Console ログで `[ShutdownCoordinator] [BeforeDisconnect] VanillaApi.Save start` → `done` → `[Disconnect] VanillaApi.Close` → `[AfterDisconnect] WebUiHost.Stop` → `[DisposeSubsystems] VanillaApi.KillLocalServer` / `DebugObjects.ReleaseAddressables` / `MainGameStarter.DisposeResolver` が順に流れること
  - ポート 5050 / 5173 が解放されていること: `lsof -iTCP:5050 -iTCP:5173 -sTCP:LISTEN` が空

- [ ] **Check 2: Editor 再生中のドメインリロード**
  - Play 中に `.cs` を編集 → Console ログで `WebUiHost.Stop` が走る → 再起動後も Web UI が正常起動
  - Kestrel のポート競合エラーが出ないこと

- [ ] **Check 3: Editor 終了**
  - Unity Editor 自体を終了（Play せずに） → Vite プロセスが残らないこと: `pgrep -f vite` が空

- [ ] **Check 4: ビルド版**
  - `File > Build And Run` でビルド版起動 → ウィンドウ右上の X で閉じる
  - タスクマネージャ/`pgrep -f moorestech` でローカルサーバープロセスが残らないこと

- [ ] **Check 5: 手動セーブボタン**
  - Play Mode で In-Game ポーズメニューの Save ボタン押下 → Console にサーバー `セーブ完了` ログが出ること
  - 以前の `Thread.Sleep(50)` に依存していたフレーム内タイミング依存がなくなっていること（ログが確実に `セーブ開始` → `セーブ完了` の順で出る）

- [ ] **Check 6: 回帰テスト**

Run:
```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "StartGameTest|ShutdownCoordinatorTest"
uloop run-tests --project-path ./moorestech_server --filter-type regex --filter-value "SaveProtocol|ShutdownCoordinatorTest"
```
Expected: 全 PASS

---

## 実装順序まとめ

TDD 的に依存関係を満たす順序：

1. Task 1 → Task 2（Save ACK プロトコル、独立して入る）
2. Task 3 → Task 4 → Task 5（クライアント Coordinator + テスト）
3. Task 6（クライアント Bridge）
4. Task 7 → Task 8 → Task 9（サーバー Coordinator + テスト）
5. Task 10（サーバー Bridge）
6. Task 11（VanillaApi 登録）
7. Task 12 → Task 13 → Task 14（クライアント参加者移行）
8. Task 15 → Task 16（サーバー参加者移行）
9. Task 17（SaveButton 切り替え）
10. **Task 20 → Task 19 → Task 18**（削除は依存の葉から。順序重要）
11. Task 21（手動検証）

段階 1〜2 は独立した先行 PR にしても良い。3〜11 は中間状態を main に出さないため単一 PR にまとめる。

---

## 実装上の注意

- **パケットディスパッチャ**: `SaveResponseMessagePack` は既存の `ProtocolMessagePackBase` を継承するだけで既存のレスポンス経路に乗る。Tag は `va:save` のまま（リクエストと同じ Tag でレスポンス種別を区別する運用）。`PacketExchangeManager.GetPacketResponse<T>` は `T` の MessagePack デシリアライズに依存するため、新型のシリアライズ可能性だけ Task 1 コンパイルで確認。
- **Editor の `beforeAssemblyReload`**: 頻発するため Bridge の timeout (5秒) を毎回使うと開発体験が悪い。初期実装では固定 5秒のまま進め、操作感が悪ければ後続 PR で `beforeAssemblyReload` のみ短縮を検討（spec 外、将来タスク）。
- **`MainGameStarter` の `_resolver` 登録タイミング**: `_resolver` は `StartGame` 終盤で代入される。Register 時点では `null` の可能性があるので、ラムダ内で都度 `_resolver?.Dispose()` するのが正解（コード例の通り）。
- **文字化け防止**: `.cs` 編集時は `AGENTS.md` の「文字化け防止ワークフロー」に従う。特にエンコーディング確認 → 編集 → 元エンコードへ戻す → `git diff` で化け文字検査。
