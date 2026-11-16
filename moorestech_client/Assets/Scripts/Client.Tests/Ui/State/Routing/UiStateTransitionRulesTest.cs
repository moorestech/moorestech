using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Routing
{
    public class UiStateTransitionRulesTest
    {
        [TestCase(UIStateEnum.SubInventory, UIStateEnum.GameScreen, true)]
        [TestCase(UIStateEnum.SubInventory, UIStateEnum.PlayerInventory, false)]
        [TestCase(UIStateEnum.SubInventory, UIStateEnum.SubInventory, true)]
        public void Webの許可表と同じ判定(UIStateEnum current, UIStateEnum requested, bool expected)
        {
            Assert.AreEqual(expected, UiStateTransitionRules.IsAllowed(current, requested));
        }

        [TestCase(UIStateEnum.Story)]
        [TestCase(UIStateEnum.DeleteBar)]
        public void 表に無い現状態からは拒む(UIStateEnum current)
        {
            Assert.IsFalse(UiStateTransitionRules.IsAllowed(current, UIStateEnum.GameScreen));
        }
    }
}
