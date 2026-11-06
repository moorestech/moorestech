using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.UIState;
using Client.Ui.State.ModeHud;
using Game.Blueprint;
using NUnit.Framework;

namespace Client.Tests.Ui.State.ModeHud
{
    /// <summary>
    /// UI 状態で派生の型が決まり、削除箱は高さだけを持つ（L1。Web の PlacementModeHud.localization.test.ts）
    /// The UI state decides the derived type and the delete box carries only the height (L1; Web PlacementModeHud.localization.test.ts)
    /// </summary>
    public class ModeHudViewModelFactoryTest
    {
        [TestCase(UIStateEnum.PlaceBlock, typeof(ModeHudPlacement))]
        [TestCase(UIStateEnum.DeleteBar, typeof(ModeHudDelete))]
        [TestCase(UIStateEnum.GameScreen, typeof(ModeHudHidden))]
        [TestCase(UIStateEnum.PlayerInventory, typeof(ModeHudHidden))]
        public void UI状態が設置なら配置削除なら削除それ以外は出さない(UIStateEnum state, Type expected)
        {
            Assert.IsInstanceOf(expected, ModeHudViewModelFactory.Create(state, null, 0));
        }

        /// <web-test file="src/features/modeHud/PlacementModeHud.localization.test.ts">削除箱は共用高さの更新だけを表示する</web-test>
        [Test]
        public void 削除箱は対象を捨て共用の高さだけを持つ()
        {
            var guid = Guid.NewGuid();
            var target = new BlueprintPlacementTarget(guid, "STALE TARGET", new BlueprintJsonObject("STALE TARGET", new(), new(), new(), guid));
            var viewModel = ModeHudViewModelFactory.Create(UIStateEnum.DeleteBar, target, 3);
            Assert.AreEqual(new ModeHudDelete(3), viewModel);
            Assert.AreEqual(new ModeHudDelete(-1), ModeHudViewModelFactory.Create(UIStateEnum.DeleteBar, target, -1));
        }

        [Test]
        public void 対象が未選択なら名前は無いを型で持つ()
        {
            var viewModel = (ModeHudPlacement)ModeHudViewModelFactory.Create(UIStateEnum.PlaceBlock, null, 2);
            Assert.IsFalse(viewModel.Target.HasValue);
            Assert.AreEqual(viewModel, ModeHudViewModelFactory.Create(UIStateEnum.PlaceBlock, null, 2), "同じ値の組は等しい");
        }
    }
}
