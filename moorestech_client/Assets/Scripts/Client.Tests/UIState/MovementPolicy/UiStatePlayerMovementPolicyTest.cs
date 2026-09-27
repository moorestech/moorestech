using System;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.MovementPolicy;
using NUnit.Framework;

namespace Client.Tests.UIState.MovementPolicy
{
    public class UiStatePlayerMovementPolicyTest
    {
        [Test]
        public void メニュー画面だけが移動を止める()
        {
            var expectedMenuScreens = new[]
            {
                UIStateEnum.PlayerInventory,
                UIStateEnum.SubInventory,
                UIStateEnum.PauseMenu,
                UIStateEnum.ChallengeList,
                UIStateEnum.ResearchTree,
                UIStateEnum.BuildMenu,
            };

            // 全state分類、未分類追加は例外で検知
            // Classify all states; unclassified ones throw
            var actualMenuScreens = Enum.GetValues(typeof(UIStateEnum)).Cast<UIStateEnum>()
                .Where(UiStatePlayerMovementPolicy.IsMenuScreen)
                .ToArray();

            CollectionAssert.AreEquivalent(expectedMenuScreens, actualMenuScreens);
        }
    }
}
