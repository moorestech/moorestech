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
    /// 解放による並べ直しの下で、長押し（マウスと Enter）とアンカーの付け替えを確かめる（L1）
    /// Verifies the hold (mouse and Enter) and the anchor move across an unlock relayout (L1)
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
        public void 解放で並びが変わっても既存のエントリは同じ要素で長押しが続く()
        {
            var view = MountInRules(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var guid = RecipeFixtures.RecipeGuid(2);
            view.Render(Selected(7, CraftVm(guid, 0.4, true, false, Material(1, 1, 9))));
            simulate.FrameUpdate();
            var entry = view.Q($"craft-recipe-entry-{guid}");
            simulate.MouseDown(entry.Q<RecipeActionButton>().worldBound.center);
            for (var i = 0; i < 3; i++) simulate.FrameUpdateMs(100);

            // 後ろに別レシピが解放された（押している要素の位置は動かない）
            // Another recipe unlocks behind it (the pressed element does not move)
            view.Render(Selected(7, CraftVm(guid, 0.4, true, false, Material(1, 1, 9)), CraftVm(RecipeFixtures.RecipeGuid(3), 0.4, true, false, Material(1, 1, 9))));
            Assert.AreSame(entry, view.Q($"craft-recipe-entry-{guid}"));

            // 合計 0.5 秒前後（初回の経過は 0〜0.25 秒）で 1 回＝SetEntries の付け外しを越えて経過を保った
            // About 0.5 s in total (first delta 0 to 0.25 s) crafts once: progress survived the SetEntries detach and re-attach
            for (var i = 0; i < 2; i++) simulate.FrameUpdateMs(100);
            CollectionAssert.AreEqual(new[] { guid }, _commands.Crafted);
        }

        [Test]
        public void マウス保持中に並べ直してから離すと止まる()
        {
            // 付け外しでボタンの捕捉が外れても、離しは同じ位置のボタンに届いて長押しが止まる（Task 6 レビューの申し送り）
            // Even if the detach drops the button's capture, the release reaches the button at the same spot and stops the hold (Task 6 review follow-up)
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
            Assert.Greater(ArrowProgress(view, guid), 0f);
            simulate.MouseUp(button.worldBound.center);
            Assert.AreEqual(0f, ArrowProgress(view, guid));
            for (var i = 0; i < 10; i++) simulate.FrameUpdateMs(100);
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
