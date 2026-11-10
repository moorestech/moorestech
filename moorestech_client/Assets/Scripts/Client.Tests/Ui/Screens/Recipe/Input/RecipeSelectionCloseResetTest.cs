using Client.Ui.Screens.Recipe.Input;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Routing;
using Core.Master;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.Screens.Recipe.Input
{
    /// <summary>
    /// 遷移と同じフレームの CloseUI でだけ選択を外す（L0）
    /// The selection is cleared only by CloseUI in the same frame as the transition (L0)
    /// </summary>
    public class RecipeSelectionCloseResetTest
    {
        private ReactiveProperty<UiRoutingSnapshot> _routing;
        private FakeCloseKeyProbe _probe;
        private RecipeItemSelection _selection;
        private RecipeSelectionCloseReset _reset;

        [SetUp]
        public void SetUp()
        {
            _routing = new ReactiveProperty<UiRoutingSnapshot>(new UiRoutingSnapshot(UiScreen.PlayerInventory));
            _probe = new FakeCloseKeyProbe();
            _selection = new RecipeItemSelection();
            _reset = new RecipeSelectionCloseReset(_routing, _probe, _selection);
            _reset.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _reset.Dispose();
        }

        [Test]
        public void 画面が変わったフレームにCloseUIが押されていれば選択を外す()
        {
            _selection.Select(new ItemId(3));
            _probe.SetPressed(true);
            _routing.Value = new UiRoutingSnapshot(UiScreen.None);
            Assert.IsInstanceOf<NoRecipeSelection>(_selection.Current.Value);
        }

        [Test]
        public void CloseUIなしで閉じたときは選択を残す()
        {
            _selection.Select(new ItemId(3));
            _probe.SetPressed(false);
            _routing.Value = new UiRoutingSnapshot(UiScreen.None);
            Assert.IsInstanceOf<ItemRecipeSelection>(_selection.Current.Value);
        }

        [Test]
        public void 画面が変わらなければCloseUIが押されていても選択を残す()
        {
            // 購読直後の現在値（Skip(1) で捨てる初回）では外さない
            // The current value replayed on subscribe (dropped by Skip(1)) does not clear
            _selection.Select(new ItemId(3));
            _probe.SetPressed(true);
            Assert.IsInstanceOf<ItemRecipeSelection>(_selection.Current.Value);
        }
    }
}
