using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Ui.State.Commands;
using Client.Ui.State.Research;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Game.Research;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Research.Wiring
{
    /// <summary>
    /// 研究の応答を状態へ反映し結果へ写す（L0。トーストは呼び手の View が出す〈ResearchDetailActionsToastTest〉。送信は録画が見る）
    /// Applies the research response and maps it to a result (L0; the calling view toasts it, see ResearchDetailActionsToastTest; the send is covered by the recording)
    /// </summary>
    public class ResearchTreeCommandsTest
    {
        private static readonly Guid Target = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        private ResearchNodeStateStore _store;

        [SetUp]
        public void SetUp()
        {
            // Store は Initialize せず反映の口だけ使う
            // The store is used without Initialize, only its apply port
            _store = new ResearchNodeStateStore(null, null);
        }

        [Test]
        public void 成功の応答は状態を反映し受理にする()
        {
            var result = ResearchTreeCommands.ToCommandResult(Response(true, ResearchNodeState.Completed), _store, Target);

            Assert.AreSame(UiCommandAccepted.Instance, result);
            Assert.AreEqual(ResearchNodeState.Completed, _store.NodeStates[Target]);
            Assert.AreEqual(ResearchNodeState.Researchable, _store.NodeStates[Other]);
        }

        [Test]
        public void 失敗の応答は状態を反映し研究失敗の拒否にする()
        {
            var result = ResearchTreeCommands.ToCommandResult(Response(false, ResearchNodeState.UnresearchableNotEnoughItem), _store, Target);

            Assert.AreEqual(UiCommandRejectReason.ResearchFailed, RejectReasonOf(result));
            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, _store.NodeStates[Target]);
        }

        [Test]
        public void 失敗でも当該ノードが完了済みなら二度押しとみなし受理にする()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"already completed"));

            var result = ResearchTreeCommands.ToCommandResult(Response(false, ResearchNodeState.Completed), _store, Target);

            Assert.AreSame(UiCommandAccepted.Instance, result);
            Assert.AreEqual(ResearchNodeState.Completed, _store.NodeStates[Target]);
        }

        [Test]
        public void 応答なしは状態を保ち研究失敗の拒否にする()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"returned no response"));

            var result = ResearchTreeCommands.ToCommandResult(null, _store, Target);

            Assert.AreEqual(UiCommandRejectReason.ResearchFailed, RejectReasonOf(result));
            Assert.IsEmpty(_store.NodeStates);
        }

        private static UiCommandRejectReason RejectReasonOf(UiCommandResult result)
        {
            Assert.IsInstanceOf<UiCommandRejected>(result);
            return ((UiCommandRejected)result).Reason;
        }

        private static CompleteResearchProtocol.ResponseCompleteResearchMessagePack Response(bool success, ResearchNodeState targetState)
        {
            var states = new Dictionary<Guid, ResearchNodeState> { { Target, targetState }, { Other, ResearchNodeState.Researchable } };
            return new CompleteResearchProtocol.ResponseCompleteResearchMessagePack(success, Target.ToString(), states);
        }
    }
}
