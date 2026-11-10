using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Screens.Recipe;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// ビューアのパネルの寸法・並び・作り直しを画面の USS の下で確かめる（L1）
    /// Verifies the viewer panel's geometry, order and rebuilds under the screen USS (L1)
    /// </summary>
    public class RecipeViewerViewTest : ScreenTestHost
    {
        private RecordingRecipeCommands _commands;
        private RecipeItemSelection _selection;

        [SetUp]
        public void SetUpViewer()
        {
            _commands = new RecordingRecipeCommands();
            _selection = new RecipeItemSelection();
        }

        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">正本のヘッダ装飾、1段時の無スクロールバー、主要構造を保つ</web-test>
        [Test]
        public void ヘッダの装飾線と菱形があり1件ならバーが出ずグリップは中身に重ならない()
        {
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, true, Material(1, 1, 3))));
            simulate.FrameUpdate();

            // レイアウトは整数の px へ丸めるので 276.743 は 276 か 277 に解決する（LX-42 と同じ丸め）
            // Layout snaps to whole px, so 276.743 resolves to 276 or 277 (the same snapping as LX-42)
            var rule = view.Q<ItemHeaderRule>();
            Assert.AreEqual(276.743f, rule.resolvedStyle.width, 1f);
            Assert.AreEqual(9f, rule.resolvedStyle.height, 0.5f);
            var diamond = rule.Q(className: "item-header__diamond");
            Assert.AreEqual(rule.worldBound.center.x, diamond.worldBound.center.x, 0.5f);
            Assert.AreEqual(rule.worldBound.center.y, diamond.worldBound.center.y, 0.5f);
            var scroll = view.Q<GameScrollView>("recipe-entry-list-scroll");
            Assert.AreEqual(DisplayStyle.None, scroll.verticalScroller.resolvedStyle.display);
            var grip = view.Q(className: "game-panel__craft-grip");
            Assert.IsFalse(grip.worldBound.Overlaps(view.Q("recipe-entry-list").worldBound));
            Assert.AreEqual(432.983f, view.Q<GamePanel>("recipe-viewer").resolvedStyle.height, 0.5f);
        }

        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">複数レシピはクラフト優先の単一リストで同時に表示される</web-test>
        [Test]
        public void 複数レシピはクラフトが先の1本の列に並ぶ()
        {
            // 本番の組み立てに機械を先に渡しても、列はクラフト 2 件 → 機械の順
            // Even with the machine passed first to the production factory, the list is two crafts then the machine
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var crafts = new[] { RecipeFixtures.Craft(RecipeFixtures.RecipeGuid(1), 7, 1, 1, RecipeFixtures.Amount(1, 1)), RecipeFixtures.Craft(RecipeFixtures.RecipeGuid(2), 7, 1, 1, RecipeFixtures.Amount(1, 1)) };
            var machines = new[] { RecipeFixtures.Machine(RecipeFixtures.RecipeGuid(3), 7, 1, RecipeLevelInheritance.None) };
            view.Render(RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), crafts, machines, new Dictionary<ItemId, int>()));

            var names = view.Q("recipe-entry-list").Children().Select(child => child.name).ToArray();
            CollectionAssert.AreEqual(new[] { $"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}", $"craft-recipe-entry-{RecipeFixtures.RecipeGuid(2)}", $"machine-recipe-entry-{RecipeFixtures.RecipeGuid(3)}" }, names);
        }

        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">クラフトエントリが複数でもチュートリアルアンカーは1件だけ付く</web-test>
        [Test]
        public void クラフトが複数でもアンカーは1件()
        {
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            view.Render(Selected(7, ManyCrafts(3)));
            var resolved = TutorialAnchorRegistry.Resolve(view, AnchorIds.RecipeCraftButton);
            Assert.IsInstanceOf<TutorialAnchorResolution.Unique>(resolved);
            Assert.AreSame(view.Q($"craft-button-{RecipeFixtures.RecipeGuid(1)}"), ((TutorialAnchorResolution.Unique)resolved).Element);
        }

        /// <web-test file="e2e/tests/recipe/recipeListScroll.spec.ts">レシピが溢れてもリストの視口高は変わらず、縦バーだけが出る</web-test>
        [Test]
        public void 溢れても視口の高さは同じで縦バーだけが出る()
        {
            var one = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            one.Render(Selected(7, ManyCrafts(1)));
            simulate.FrameUpdate();
            var oneViewport = one.Q<GameScrollView>("recipe-entry-list-scroll").GetViewportHeight();
            one.RemoveFromHierarchy();

            var many = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            many.Render(Selected(7, ManyCrafts(8)));
            simulate.FrameUpdate();
            var scroll = many.Q<GameScrollView>("recipe-entry-list-scroll");
            Assert.Greater(oneViewport, 0f);
            Assert.AreEqual(oneViewport, scroll.GetViewportHeight(), 0.5f);
            Assert.AreEqual(DisplayStyle.Flex, scroll.verticalScroller.resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, scroll.horizontalScroller.resolvedStyle.display);
        }

        [Test]
        public void 未選択は案内で高さ300に戻り選択を変えるとスクロールが先頭に戻り同じGUIDの並びでは要素を作り直さない()
        {
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            view.Render(RecipeViewerNoSelection.Instance);
            simulate.FrameUpdate();
            Assert.AreEqual(300f, view.Q<GamePanel>("recipe-viewer").resolvedStyle.height, 0.5f);
            Assert.AreEqual(DisplayStyle.Flex, view.Q<Label>("recipe-viewer-hint").resolvedStyle.display);

            view.Render(Selected(7, ManyCrafts(8)));
            var first = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}");
            view.Render(Selected(7, ManyCrafts(8)));
            Assert.AreSame(first, view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}"));
            Assert.AreEqual(DisplayStyle.None, view.Q<Label>("recipe-viewer-hint").resolvedStyle.display);

            // 溢れた列を 80 まで送ってから別のアイテムを選ぶと先頭へ戻る（送れたことを先に確かめる）
            // Scroll the overflowing list to 80, then select another item and it returns to the top (check the scroll took first)
            simulate.FrameUpdate();
            view.Q<GameScrollView>("recipe-entry-list-scroll").SetScrollOffset(80);
            Assert.AreEqual(80f, view.Q<GameScrollView>("recipe-entry-list-scroll").scrollOffset.y, 0.5f);
            view.Render(Selected(8, ManyCrafts(8)));
            simulate.FrameUpdate();
            Assert.AreEqual(0f, view.Q<GameScrollView>("recipe-entry-list-scroll").scrollOffset.y, 0.5f);
        }

        [Test]
        public void 隠して出し直すと同じ選択でも中身を作り直す()
        {
            // Web は画面を出るとアンマウントするので、開き直すと長押しもスクロールも初期化される
            // Web unmounts on leaving the screen, so reopening resets the hold and the scroll
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            view.Render(Selected(7, ManyCrafts(8)));
            var first = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}");
            view.SetShown(false);
            Assert.IsNull(first.panel);

            // 隠れている間の描き直しは覚えるだけで、出し直すと Render を呼ばずに最新で組む
            // Redraws while hidden are only remembered; showing again builds from the latest without another Render
            view.Render(Selected(8, ManyCrafts(2)));
            Assert.IsNull(view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}"));
            view.SetShown(true);
            var again = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}");
            Assert.IsNotNull(again);
            Assert.AreNotSame(first, again);
            Assert.AreEqual(2, view.Q("recipe-entry-list").childCount);
        }

        [Test]
        public void 同じ選択のままレシピ0件から1件になると列が出る()
        {
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            view.Render(Selected(7));
            Assert.IsNotNull(view.Q<Label>("recipe-viewer-no-recipes"));
            view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, false, Material(1, 1, 3))));
            Assert.IsNull(view.Q<Label>("recipe-viewer-no-recipes"));
            Assert.IsNotNull(view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}"));
            view.Render(Selected(7));
            Assert.IsNotNull(view.Q<Label>("recipe-viewer-no-recipes"));
        }

        [Test]
        public void 最初のエントリは見出しの装飾線の下にgapと列のbleedの分だけ空く()
        {
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            view.Render(Selected(7, CraftVm(RecipeFixtures.RecipeGuid(1), 2, true, false, Material(1, 1, 3))));
            simulate.FrameUpdate();
            var ruleBottom = view.Q<ItemHeaderRule>().worldBound.yMax;
            var entryTop = view.Q($"craft-recipe-entry-{RecipeFixtures.RecipeGuid(1)}").worldBound.yMin;

            // Web: Stack gap sm 12 − margin 12 ＋ 視口の padding 12 ＋ 列の bleed 5（--recipe-entry-bleed）
            // Web: Stack gap sm 12 - margin 12 + viewport padding 12 + list bleed 5 (--recipe-entry-bleed)
            Assert.AreEqual(17f, entryTop - ruleBottom, 0.5f);
        }

        [Test]
        public void 機械の名前の下はバッジまで2px()
        {
            // 画面の USS が名前の既定の margin を 0 にし、Web <Stack gap={2}> の間隔だけが残る
            // The screen USS zeroes the name's default margin, leaving only the Web <Stack gap={2}> spacing
            var view = Mount(new RecipeViewerView(Context(Toaster, _commands, _selection)));
            var guid = RecipeFixtures.RecipeGuid(4);
            var inheriting = RecipeLevelInheritance.Inherit(0, RecipeFixtures.Ids(1), 0, RecipeFixtures.Ids(2), 3);
            view.Render(Selected(7, new MachineRecipeEntryViewModel(RecipeFixtures.Machine(guid, 7, 1, inheriting))));
            simulate.FrameUpdate();
            var entry = view.Q($"machine-recipe-entry-{guid}");
            var name = entry.Q(className: "machine-recipe-entry__block-name");
            var badge = entry.Q(className: "level-inheritance-badge");
            Assert.AreEqual(2f, badge.worldBound.yMin - name.worldBound.yMax, 0.01f);
            Assert.AreEqual(12f, name.worldBound.height, 0.01f);
        }
    }
}
