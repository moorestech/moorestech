using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using NUnit.Framework;

namespace Client.Tests.UIState.Models
{
    public class BuildMenuSelectionTest
    {
        [Test]
        public void 選択は一度だけ消費される()
        {
            var selection = new BuildMenuSelection();
            var target = new BlueprintPlacementTarget(System.Guid.NewGuid(), "test");

            selection.SetSelectedTarget(target);

            Assert.IsTrue(selection.TryConsumeSelectedTarget(out var first));
            Assert.AreSame(target, first);
            Assert.IsFalse(selection.TryConsumeSelectedTarget(out _));
        }

        [Test]
        public void Clearで未消費の選択が捨てられる()
        {
            var selection = new BuildMenuSelection();
            selection.SetSelectedTarget(new BlueprintPlacementTarget(System.Guid.NewGuid(), "test"));

            selection.Clear();

            Assert.IsFalse(selection.TryConsumeSelectedTarget(out _));
        }
    }
}
