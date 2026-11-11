using System;
using System.Collections.Generic;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Input;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.Recipe;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// 解放による並べ直しで長押し（マウスと Enter）が止まることとアンカーの付け替えを確かめる（L1）
    /// Verifies the hold (mouse and Enter) stops and the anchor moves across an unlock relayout (L1)
    /// </summary>
    public class RecipeViewerRelayoutHoldTest : ScreenTestHost
    {
        private RecordingRecipeCommands _commands;
        private RecipeItemSelection _selection;
        private VisualElement _rulesRoot;

        [SetUp]
        public void SetUpRelayout()
        {
            // Enter の件のため規則を付けた器を用意する（前例 CraftHoldKeyTest）
            // Prepare a container with the input rules for the Enter case (precedent CraftHoldKeyTest)
            _commands = new RecordingRecipeCommands();
            _selection = new RecipeItemSelection();
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
            rootVisualElement.Add(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);
        }

        [TearDown]
        public void ResetExclusivity()
        {
            UiInputExclusivity.SetGameUiTextInputFocused(false);
        }

        [Test]
        public void 解放で並びが変わると同じ要素のまま長押しは止まり押し直しで再開する()
        {
            var view = MountInRules(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var guid = RecipeFixtures.RecipeGuid(2);
            view.Render(Selected(7, CraftVm(guid, 1, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            var entry = view.Q($"craft-recipe-entry-{guid}");
            var button = entry.Q<RecipeActionButton>();
            simulate.MouseDown(button.worldBound.center);
            for (var i = 0; i < 2; i++) simulate.FrameUpdateMs(100);
            Assert.Greater(ArrowProgress(view, guid), 0f);

            // 後ろに別レシピが解放された。要素は使い回すが、付け外しで捕捉が外れるので保持は止まる（D-34 を覆す判断記録）
            // Another recipe unlocks behind it; the element is reused, but the detach drops capture so the hold stops (overrides D-34)
            view.Render(Selected(7, CraftVm(guid, 1, true, false, Material(1, 1, 9)), CraftVm(RecipeFixtures.RecipeGuid(3), 1, true, false, Material(1, 1, 9))));
            Assert.AreSame(entry, view.Q($"craft-recipe-entry-{guid}"));
            Assert.AreEqual(0f, ArrowProgress(view, guid));
            for (var i = 0; i < 5; i++) simulate.FrameUpdateMs(100);
            Assert.IsEmpty(_commands.Crafted);

            // 押し直すと始めから進み、合計 1 秒前後（初回の経過は 0〜0.25 秒）で 1 回作る
            // A re-press starts from zero and crafts once in about 1 s (first delta 0 to 0.25 s)
            simulate.MouseUp(button.worldBound.center);
            simulate.MouseDown(button.worldBound.center);
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
            CollectionAssert.AreEqual(new[] { guid }, _commands.Crafted);
        }

        [Test]
        public void マウス保持中に並べ直すとその場で止まり離しても作らない()
        {
            // 付け外しで保持を止めるので、離しが届くかに頼らない（Task 6 レビューの申し送り。D-34 を覆す判断記録）
            // The detach ends the hold, so it does not rely on the release arriving (Task 6 review follow-up; overrides D-34)
            // 1 回の所要は 1 秒にし、離す前の経過（高々 0.35＋0.25 秒）でクラフトしないようにする
            // Use a 1 s cycle so the time before the release (at most 0.35 + 0.25 s) never crafts
            var view = MountInRules(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var guid = RecipeFixtures.RecipeGuid(2);
            view.Render(Selected(7, CraftVm(guid, 1, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            var button = view.Q<RecipeActionButton>($"craft-button-{guid}");
            simulate.MouseDown(button.worldBound.center);
            for (var i = 0; i < 2; i++) simulate.FrameUpdateMs(100);
            Assert.Greater(ArrowProgress(view, guid), 0f);

            view.Render(Selected(7, CraftVm(guid, 1, true, false, Material(1, 1, 9)), CraftVm(RecipeFixtures.RecipeGuid(3), 1, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            Assert.AreEqual(0f, ArrowProgress(view, guid));
            simulate.MouseUp(button.worldBound.center);
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
            Assert.IsEmpty(_commands.Crafted);
        }

        [Test]
        public void マウス保持中に前へ解放されボタンがずれてから元の位置で離しても止まる()
        {
            // 前方追加で押したボタンが下へずれ、元の位置で離すと離しは別のボタンへ落ちる。それでもクラフトが続かない
            // A front unlock shifts the pressed button down, so a release at the old spot lands elsewhere; crafting still does not continue
            var view = MountInRules(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var guid = RecipeFixtures.RecipeGuid(2);
            view.Render(Selected(7, CraftVm(guid, 1, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            var button = view.Q<RecipeActionButton>($"craft-button-{guid}");
            var pressedAt = button.worldBound.center;
            simulate.MouseDown(pressedAt);
            for (var i = 0; i < 2; i++) simulate.FrameUpdateMs(100);
            Assert.Greater(ArrowProgress(view, guid), 0f);

            view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 1, true, false, Material(1, 1, 9)), CraftVm(guid, 1, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            Assert.AreNotEqual(pressedAt, button.worldBound.center);
            Assert.AreEqual(0f, ArrowProgress(view, guid));
            simulate.MouseUp(pressedAt);
            for (var i = 0; i < 15; i++) simulate.FrameUpdateMs(100);
            Assert.IsEmpty(_commands.Crafted);
        }

        [Test]
        public void 解放で先頭のクラフトが変わってもアンカーは新しい先頭へ移り旧先頭の要素は作り直さない()
        {
            // 本番の組み立て（RecipeViewModelFactory.BuildViewer）で「B だけ → A・B」の VM を作る。A は B より前に並ぶレシピ
            // Build "B only -> A and B" with the production factory; A sorts before B
            var view = MountInRules(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var a = RecipeFixtures.Craft(RecipeFixtures.RecipeGuid(1), 7, 1, 0.4, RecipeFixtures.Amount(1, 1));
            var b = RecipeFixtures.Craft(RecipeFixtures.RecipeGuid(2), 7, 1, 0.4, RecipeFixtures.Amount(1, 1));
            var owned = new Dictionary<ItemId, int> { { new ItemId(1), 9 } };
            view.Render(RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), new[] { b }, Array.Empty<MachineRecipeData>(), owned));
            var bEntry = view.Q($"craft-recipe-entry-{b.RecipeGuid}");

            view.Render(RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), new[] { a, b }, Array.Empty<MachineRecipeData>(), owned));
            Assert.AreSame(bEntry, view.Q($"craft-recipe-entry-{b.RecipeGuid}"));
            var resolved = TutorialAnchorRegistry.Resolve(view, AnchorIds.RecipeCraftButton);
            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(resolved);
            Assert.AreSame(view.Q($"craft-button-{a.RecipeGuid}"), ((TutorialAnchorResolution.Unique)resolved).Element);
        }

        [Test]
        public void Enterで長押し中に並びが変わると止まる()
        {
            var view = MountInRules(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var guid = RecipeFixtures.RecipeGuid(2);
            view.Render(Selected(7, CraftVm(guid, 0.4, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            var button = view.Q<RecipeActionButton>($"craft-button-{guid}");
            simulate.Click(button.worldBound.center);
            SendKeyDown(button, KeyCode.Return);
            for (var i = 0; i < 2; i++) simulate.FrameUpdateMs(100);
            Assert.Greater(ArrowProgress(view, guid), 0f);

            // 前に別レシピが解放され付け直しでフォーカスが外れる。KeyUp が届かなくても保持し続けない
            // A recipe unlocks in front and the re-attach drops focus; without a KeyUp the hold still does not persist
            view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 0.4, true, false, Material(1, 1, 9)), CraftVm(guid, 0.4, true, false, Material(1, 1, 9))));
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
            Assert.IsFalse(_commands.Crafted.Contains(guid));
        }

        private T MountInRules<T>(T view) where T : VisualElement
        {
            _rulesRoot.Add(view);
            simulate.FrameUpdate();
            return view;
        }

        private static void SendKeyDown(VisualElement target, KeyCode keyCode)
        {
            using var evt = KeyDownEvent.GetPooled('\0', keyCode, EventModifiers.None);
            target.SendEvent(evt);
        }
    }
}
