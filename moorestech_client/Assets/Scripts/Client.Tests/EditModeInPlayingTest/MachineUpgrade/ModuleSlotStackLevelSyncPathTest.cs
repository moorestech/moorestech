using System;
using System.Collections;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.MachineUpgrade;
using Cysharp.Threading.Tasks;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive.MachineUpgrade;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UniRx;
using VContainer;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.MachineUpgrade
{
    /// <summary>
    ///     スロット上限レベルの初期同期（handshake）とイベント購読を実起動の配線で通し、範囲外の値は警告して現在値を保ち、正しい値で戻ることを検査する（D09）
    ///     Runs the slot-limit level's initial sync (handshake) and event subscription through the real boot wiring, checking out-of-range values warn and keep the current value and a valid one restores it (D09)
    /// </summary>
    [Category("CiShardClientPlay1")]
    public class ModuleSlotStackLevelSyncPathTest
    {
        private const string InvalidLevelWarningPrefix = "Invalid module slot stack level: ";
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

        [UnityTest]
        public IEnumerator NegativeHandshakeLevelWarnsAndEventPathRecoversTest()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return RunSyncPath(-1).ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTest]
        public IEnumerator AboveMaxHandshakeLevelWarnsAndEventPathRecoversTest()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return RunSyncPath(AboveMaxLevel).ToCoroutine();

            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        // テスト用マスタの最大レベル（1）を超える値。起動後に最大レベルを超えていることを確かめる
        // A value above the test master's max level (1); checked against the max level after boot
        private const int AboveMaxLevel = 1000;

        private static async UniTask RunSyncPath(int handshakeLevel)
        {
            var warnings = new List<string>();
            Application.logMessageReceived += CollectWarning;

            // 実起動で handshake の値を範囲外へ差し替え、製品の初期化に適用させる
            // Swap the handshake value out of range during the real boot and let the product initialization apply it
            var injector = HandshakeModuleSlotStackLevelInjector.Install(handshakeLevel);
            await LoadMainGame();
            Assert.IsTrue(injector.Injected, "the handshake was not intercepted before initialization");
            var maxLevel = ModuleSlotStackLimitMasterUtil.GetMaxLevel();
            Assert.IsTrue(handshakeLevel < 0 || maxLevel < handshakeLevel, $"injected level {handshakeLevel} must be out of 0..{maxLevel}");

            // 初期化と購読が見るミラーは lookup と mutation で同じ実体
            // The mirror seen by initialization and subscription is the same instance behind lookup and mutation
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var lookup = resolver.Resolve<IModuleSlotStackLimitLookup>();
            Assert.AreSame(lookup, resolver.Resolve<IClientModuleSlotStackLimitMutation>());
            Assert.IsInstanceOf<ClientModuleSlotStackLimitDatastore>(lookup);

            // 範囲外の初期値は警告して捨て、既定の0のまま
            // An out-of-range initial value is warned and dropped, leaving the default 0
            Assert.Contains(InvalidLevelWarningPrefix + handshakeLevel, warnings);
            Assert.AreEqual(0, lookup.CurrentLevel);

            // サーバーから実イベント経路で正しい値を配ると、実 MachineUpgradeEventHandler が反映する
            // Broadcasting a valid value over the real event path lets the real MachineUpgradeEventHandler apply it
            var changes = new List<int>();
            using var subscription = lookup.OnStackLimitChanged.Subscribe(changes.Add);
            Broadcast(maxLevel);
            await WaitObserved(UniTask.WaitUntil(() => lookup.CurrentLevel == maxLevel), maxLevel);
            Assert.AreEqual(ModuleSlotStackLimitMasterUtil.GetStackLimit(maxLevel), lookup.CurrentStackLimit);
            CollectionAssert.AreEqual(new[] { ModuleSlotStackLimitMasterUtil.GetStackLimit(maxLevel) }, changes);

            // 範囲外のイベント（負値・最大超過）は警告して現在値を保つ
            // Out-of-range events (negative, above max) warn and keep the current value
            Broadcast(-1);
            await WaitObserved(UniTask.WaitUntil(() => warnings.Contains(InvalidLevelWarningPrefix + -1)), -1);
            Broadcast(maxLevel + 1);
            await WaitObserved(UniTask.WaitUntil(() => warnings.Contains(InvalidLevelWarningPrefix + (maxLevel + 1))), maxLevel + 1);
            Assert.AreEqual(maxLevel, lookup.CurrentLevel);
            Assert.AreEqual(1, changes.Count);

            // 正しい値（0）で戻る
            // A valid value (0) restores it
            Broadcast(0);
            await WaitObserved(UniTask.WaitUntil(() => lookup.CurrentLevel == 0), 0);
            Assert.AreEqual(ModuleSlotStackLimitMasterUtil.GetStackLimit(0), lookup.CurrentStackLimit);

            Application.logMessageReceived -= CollectWarning;

            #region Internal

            void CollectWarning(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition.StartsWith(InvalidLevelWarningPrefix)) warnings.Add(condition);
            }

            #endregion
        }

        // 製品と同じ配信口（EventProtocolProvider の全員配信）へ MessagePack のイベントを積む
        // Queue the MessagePack event on the product's own channel (EventProtocolProvider broadcast)
        private static void Broadcast(int level)
        {
            var payload = MessagePackSerializer.Serialize(new ModuleSlotStackLevelEventPacket.ModuleSlotStackLevelMessagePack(level));
            ServerContext.GetService<EventProtocolProvider>().AddBroadcastEvent(ModuleSlotStackLevelEventPacket.EventTag, payload);
        }

        // クライアントで観測できるまで待ち、期限を過ぎたら失敗にする
        // Wait until the client observes it and fail once the deadline passes
        private static async UniTask WaitObserved(UniTask observed, int level)
        {
            var arrived = await UniTask.WhenAny(observed, UniTask.Delay(EventTimeout));
            Assert.AreEqual(0, arrived, $"module slot stack level event {level} was not observed within {EventTimeout.TotalSeconds}s");
        }
    }
}
