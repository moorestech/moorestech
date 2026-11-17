using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Routing
{
    public class UiStateTransitionRulesTest
    {
        // 同じ状態どうし以外で許す 7 組。遷移の表を変えたらここも直す
        // The seven allowed pairs besides same-state; update this when the transition table changes
        private static readonly HashSet<(UIStateEnum current, UIStateEnum requested)> AllowedPairs = new()
        {
            (UIStateEnum.GameScreen, UIStateEnum.PlayerInventory),
            (UIStateEnum.PlayerInventory, UIStateEnum.GameScreen),
            (UIStateEnum.SubInventory, UIStateEnum.GameScreen),
            (UIStateEnum.BuildMenu, UIStateEnum.GameScreen),
            (UIStateEnum.ResearchTree, UIStateEnum.GameScreen),
            (UIStateEnum.ChallengeList, UIStateEnum.GameScreen),
            (UIStateEnum.PauseMenu, UIStateEnum.GameScreen),
        };

        [TestCase(UIStateEnum.SubInventory, UIStateEnum.GameScreen, true)]
        [TestCase(UIStateEnum.SubInventory, UIStateEnum.PlayerInventory, false)]
        [TestCase(UIStateEnum.SubInventory, UIStateEnum.SubInventory, true)]
        public void 代表の組の判定(UIStateEnum current, UIStateEnum requested, bool expected)
        {
            Assert.AreEqual(expected, UiStateTransitionRules.IsAllowed(current, requested));
        }

        [TestCase(UIStateEnum.Story)]
        [TestCase(UIStateEnum.DeleteBar)]
        public void 表に無い現状態からは拒む(UIStateEnum current)
        {
            Assert.IsFalse(UiStateTransitionRules.IsAllowed(current, UIStateEnum.GameScreen));
        }

        [Test]
        public void 遷移の許可表は全組でテストに書いた表と一致する()
        {
            // 12 値の全組を回し、同じ状態どうしと 7 組だけが真
            // Walk every pair of the 12 values; only same-state and the seven pairs are true
            foreach (UIStateEnum current in Enum.GetValues(typeof(UIStateEnum)))
            foreach (UIStateEnum requested in Enum.GetValues(typeof(UIStateEnum)))
            {
                var expected = current == requested || AllowedPairs.Contains((current, requested));
                Assert.AreEqual(expected, UiStateTransitionRules.IsAllowed(current, requested), $"{current} -> {requested}");
            }
        }
    }
}
