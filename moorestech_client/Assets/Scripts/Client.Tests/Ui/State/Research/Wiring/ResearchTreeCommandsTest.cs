using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Ui.State.Commands;
using Client.Ui.State.Research;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Toast;
using Game.Research;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Research.Wiring
{
    /// <summary>
    /// 研究の応答を状態へ反映し、結果のトーストを 1 か所で出す（L0。送信は録画が見る）
    /// Applies the research response and reports the result toast in one place (L0; the send itself is covered by the recording)
    /// </summary>
    public class ResearchTreeCommandsTest
    {
        private static readonly Guid Target = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid Other = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
        private ResearchNodeStateStore _store;
        private ResearchTreeCommands _commands;
        private List<GameUiToast> _shownToasts;

        [SetUp]
        public void SetUp()
        {
            // 拒否のトーストは文言を引くので言語表を読む。Store と選択は Initialize せず反映の口だけ使う
            // Rejection toasts look up text, so load the language table; the stores are used without Initialize, only their apply ports
            Localize.Initialize();
            _store = new ResearchNodeStateStore(null, null);
            var toasts = new GameUiToastFeed();
            _shownToasts = new List<GameUiToast>();
            toasts.OnToast.Subscribe(_shownToasts.Add);
            _commands = new ResearchTreeCommands(new ResearchSelectionStore(null), _store, new UiCommandResultToaster(toasts));
        }

        [Test]
        public void 成功の応答は状態を反映しトーストを出さない()
        {
            _commands.ReportResponse(Target, Response(true, ResearchNodeState.Completed));

            Assert.AreEqual(ResearchNodeState.Completed, _store.NodeStates[Target]);
            Assert.AreEqual(ResearchNodeState.Researchable, _store.NodeStates[Other]);
            Assert.IsEmpty(_shownToasts);
        }

        [Test]
        public void 失敗の応答は状態を反映し研究失敗のトーストを出す()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"research\.complete failed: research_failed"));

            _commands.ReportResponse(Target, Response(false, ResearchNodeState.UnresearchableNotEnoughItem));

            Assert.AreEqual(ResearchNodeState.UnresearchableNotEnoughItem, _store.NodeStates[Target]);
            Assert.AreEqual(1, _shownToasts.Count);
            Assert.AreEqual(GameUiToastVariant.Error, _shownToasts[0].Variant);
        }

        [Test]
        public void 失敗でも当該ノードが完了済みなら二度押しとみなしトーストを出さない()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"already completed"));

            _commands.ReportResponse(Target, Response(false, ResearchNodeState.Completed));

            Assert.AreEqual(ResearchNodeState.Completed, _store.NodeStates[Target]);
            Assert.IsEmpty(_shownToasts);
        }

        [Test]
        public void 応答なしは状態を保ち研究失敗のトーストを出す()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"returned no response"));
            LogAssert.Expect(LogType.Warning, new Regex(@"research\.complete failed: research_failed"));

            _commands.ReportResponse(Target, null);

            Assert.IsEmpty(_store.NodeStates);
            Assert.AreEqual(1, _shownToasts.Count);
        }

        [Test]
        public void 応答の写しは応答なしと失敗を研究失敗にし成功を受理にする()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"returned no response"));

            Assert.AreEqual(UiCommandRejectReason.ResearchFailed, RejectReasonOf(ResearchTreeCommands.ToCommandResult(null, _store, Target)));
            Assert.AreEqual(UiCommandRejectReason.ResearchFailed, RejectReasonOf(ResearchTreeCommands.ToCommandResult(Response(false, ResearchNodeState.UnresearchableNotEnoughItem), _store, Target)));
            Assert.AreSame(UiCommandAccepted.Instance, ResearchTreeCommands.ToCommandResult(Response(true, ResearchNodeState.Completed), _store, Target));
            Assert.AreEqual(ResearchNodeState.Completed, _store.NodeStates[Target]);
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
