using System;
using System.Collections.Generic;
using Client.Ui.State.Challenge.Tree;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Ui.State.Challenge
{
    /// <summary>
    /// チャレンジ一覧の注目点（L0）
    /// Challenge list focus (L0)
    /// </summary>
    public class ChallengeListLogicTest
    {
        private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
        private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
        private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");

        /// <web-test file="src/features/challenge/challengeLogic.test.ts">進行中(current)のノードを返す</web-test>
        [Test]
        public void 進行中のノードを返す()
        {
            var nodes = new[] { Node(A, ChallengeNodeState.Completed), Node(B, ChallengeNodeState.Current), Node(C, ChallengeNodeState.Locked) };
            Assert.IsTrue(ChallengeListLogic.TryFindInitialFocusNode(nodes, out var focus));
            Assert.AreEqual(B, focus.Guid);
        }

        /// <web-test file="src/features/challenge/challengeLogic.test.ts">進行中が無ければ寄せない</web-test>
        [Test]
        public void 進行中が無ければ寄せない()
        {
            var nodes = new[] { Node(A, ChallengeNodeState.Completed), Node(C, ChallengeNodeState.Locked) };
            Assert.IsFalse(ChallengeListLogic.TryFindInitialFocusNode(nodes, out _));
            Assert.IsFalse(ChallengeListLogic.TryFindInitialFocusNode(Array.Empty<ChallengeNodeCardViewModel>(), out _));
        }

        private static ChallengeNodeCardViewModel Node(Guid guid, ChallengeNodeState state)
        {
            return new ChallengeNodeCardViewModel(guid, Vector2.zero, Vector2.one, new List<Guid>(), state);
        }
    }
}
