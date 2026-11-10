using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Commands;
using Client.Ui.State.Recipe.Screen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// マウスの長押しクラフト（到達ごとのクラフト・外れ・可否・頭打ち）を確かめる（L1）
    /// Verifies the mouse hold-to-craft (craft per cycle, leaving, craftability, per-frame cap) (L1)
    /// </summary>
    public class CraftHoldTest : ScreenTestHost
    {
        private RecordingRecipeCommands _commands;
        private RecipeItemSelection _selection;

        [SetUp]
        public void SetUpHold()
        {
            _commands = new RecordingRecipeCommands();
            _selection = new RecipeItemSelection();
        }

        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">アイテム選択でレシピ表示、長押しで素材が尽きるまで連続クラフトする</web-test>
        [Test]
        public void 長押しで到達ごとにクラフトし可否が偽になったら止まる()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 0.4, true, false, Material(1, 1, 2)), Context(Toaster, _commands, _selection)));
            var button = view.Q<RecipeActionButton>();

            simulate.MouseDown(button.worldBound.center);
            for (var i = 0; i < 6; i++) simulate.FrameUpdateMs(100);
            Assert.AreEqual(1, _commands.Crafted.Count);
            for (var i = 0; i < 4; i++) simulate.FrameUpdateMs(100);
            CollectionAssert.AreEqual(new[] { guid, guid }, _commands.Crafted);

            // 素材が尽きた VM で描き直すと止まり矢印は 0 に戻る
            // Redrawing with exhausted materials stops the hold and resets the arrow
            view.Render(CraftVm(guid, 0.4, false, false, Material(1, 1, 0)));
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
            Assert.AreEqual(2, _commands.Crafted.Count);
            Assert.AreEqual(0f, ArrowProgress(view, guid));
        }

        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">押下後にボタンから外れるとクラフトが止まり経過時間がリセットされる</web-test>
        [Test]
        public void 押下後にボタンの外へ出ると止まり経過が0に戻る()
        {
            var guid = RecipeFixtures.RecipeGuid(1);
            var view = Mount(new CraftRecipeEntryView(CraftVm(guid, 1, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
            var button = view.Q<RecipeActionButton>();

            simulate.MouseDown(button.worldBound.center);
            for (var i = 0; i < 5; i++) simulate.FrameUpdateMs(100);
            Assert.Greater(ArrowProgress(view, guid), 0.3f);
            simulate.MouseMove(button.worldBound.center + new Vector2(0, button.worldBound.height));
            Assert.AreEqual(0f, ArrowProgress(view, guid));
            for (var i = 0; i < 20; i++) simulate.FrameUpdateMs(100);
            Assert.IsEmpty(_commands.Crafted);
        }

        [Test]
        public void 拒否はトーストへ渡り1フレームの経過は上限で頭打ち()
        {
            _commands.SetResult(new UiCommandRejected(UiCommandRejectReason.MachineRecipeLocked, ""));
            var view = Mount(new CraftRecipeEntryView(CraftVm(RecipeFixtures.RecipeGuid(1), 0.3, true, false, Material(1, 1, 9)), Context(Toaster, _commands, _selection)));
            simulate.MouseDown(view.Q<RecipeActionButton>().worldBound.center);

            // 1 フレームで 1 秒進めても 0.25 秒だけ進み、次の 0.1 秒で 0.3 秒に届いて 1 回
            // One 1-second frame advances only 0.25 s; the next 0.1 s reaches 0.3 s and crafts once
            simulate.FrameUpdateMs(1000);
            Assert.IsEmpty(_commands.Crafted);
            simulate.FrameUpdateMs(100);
            Assert.AreEqual(1, _commands.Crafted.Count);
            Assert.AreEqual(1, ShownToasts.Count);
        }
    }
}
