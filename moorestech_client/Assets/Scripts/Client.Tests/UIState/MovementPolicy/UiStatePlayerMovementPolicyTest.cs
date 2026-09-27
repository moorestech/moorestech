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

            // 全stateを分類し、未分類の追加で例外になることも同時に押さえる
            // Classify every state; an unclassified addition would throw here as well
            var actualMenuScreens = Enum.GetValues(typeof(UIStateEnum)).Cast<UIStateEnum>()
                .Where(UiStatePlayerMovementPolicy.IsMenuScreen)
                .ToArray();

            CollectionAssert.AreEquivalent(expectedMenuScreens, actualMenuScreens);
        }
    }
}
