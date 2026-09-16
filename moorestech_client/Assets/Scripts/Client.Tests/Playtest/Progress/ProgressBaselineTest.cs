using System;
using System.Collections.Generic;
using Client.Game.InGame.Playtest.Progress;
using Game.Research;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressBaselineTest
    {
        [Test]
        public void 完了済みチャレンジと完了済み研究だけを文字列で拾う()
        {
            var challengeA = Guid.NewGuid();
            var researchDone = Guid.NewGuid();
            var researchOpen = Guid.NewGuid();

            var challenges = ProgressBaseline.CompletedChallengeGuids(new List<Guid> { challengeA, challengeA });
            var research = ProgressBaseline.CompletedResearchGuids(new Dictionary<Guid, ResearchNodeState>
            {
                { researchDone, ResearchNodeState.Completed },
                { researchOpen, ResearchNodeState.Researchable },
            });

            CollectionAssert.AreEqual(new[] { challengeA.ToString() }, challenges);
            CollectionAssert.AreEqual(new[] { researchDone.ToString() }, research);
        }

        [Test]
        public void 空入力でも空リストを返す()
        {
            Assert.AreEqual(0, ProgressBaseline.CompletedChallengeGuids(new List<Guid>()).Count);
            Assert.AreEqual(0, ProgressBaseline.CompletedResearchGuids(new Dictionary<Guid, ResearchNodeState>()).Count);
        }
    }
}
