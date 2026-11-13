using System;
using Client.Localization;
using Client.Tests.Ui.State.ModeHud;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Screens.Recipe.RecipeScreenFixtures;
using static Client.Tests.Ui.State.Recipe.RecipeFixtures;

namespace Client.Tests.Ui.Screens.Recipe.Viewer
{
    /// <summary>
    /// 機械の 1 件の液体・機械名・秒数・継承バッジを確かめる（L1）
    /// Verifies one machine entry's fluids, machine name, seconds and inheritance badge (L1)
    /// </summary>
    public class MachineRecipeEntryViewTest : ScreenTestHost
    {
        private const string EnglishBlockName = "Test Machine";
        private const string JapaneseBlockName = "試験の機械";
        private RecordingRecipeCommands _commands;
        private RecipeItemSelection _selection;
        private RecipeEntryPartsContext _context;
        private PlacementNameTestDictionary _dictionary;

        [SetUp]
        public void SetUpEntry()
        {
            _commands = new RecordingRecipeCommands();
            _selection = new RecipeItemSelection();
            _context = Context(Toaster, _commands, _selection);

            // 機械名の言語追従を見るため英日の名前を合成する（基底が英語にした後。言語の切り替えでは合成は消えない）
            // Compose English/Japanese names to observe the machine-name follow (after the base picks English; switching language keeps the merge)
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(new BlockId(1)).BlockGuid;
            _dictionary = new PlacementNameTestDictionary();
            _dictionary.Merge($"{ContentLocalizationKeys.BlockName(blockGuid).Key},{EnglishBlockName},{EnglishBlockName},{JapaneseBlockName}\n");
        }

        [TearDown]
        public void TearDownEntry()
        {
            _dictionary.Restore();
        }

        /// <web-test file="src/features/recipe/views/MachineRecipeEntry.fluids.test.ts">液体の入力・出力をアイテムの後ろに量付きで描き、クリック遷移は持たせない</web-test>
        /// <web-test file="e2e/tests/recipe/recipe.spec.ts">機械レシピの一覧に液体の入力を量付きで出す</web-test>
        [Test]
        public void 液体はアイテムの後ろに量付きで矢印の左右に並び押しても選択は変わらない()
        {
            var guid = RecipeGuid(9);
            var view = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(guid, RecipeLevelInheritance.None)), _context));

            var inputFluid = view.Q($"machine-recipe-entry-{guid}-input-fluid-0");
            var outputFluid = view.Q($"machine-recipe-entry-{guid}-output-fluid-0");
            var arrow = view.Q($"machine-progress-arrow-{guid}");
            var inputItem = view.Q($"machine-recipe-entry-{guid}-input-item-0");
            Assert.Less(inputFluid.worldBound.xMax, arrow.worldBound.xMin);
            Assert.Greater(outputFluid.worldBound.xMin, arrow.worldBound.xMax);
            Assert.Less(inputItem.worldBound.xMin, inputFluid.worldBound.xMin);
            Assert.AreEqual("10", view.Q($"machine-recipe-entry-{guid}-input-fluid-0-amount").Q<Label>().text);
            Assert.AreEqual("5", view.Q($"machine-recipe-entry-{guid}-output-fluid-0-amount").Q<Label>().text);

            // 液体を押しても選択は変わらず、アイテムを押すとそのアイテムを選ぶ（Web onLeftDown はアイテムだけ）
            // Pressing a fluid keeps the selection; pressing an item selects it (the Web onLeftDown is on items only)
            simulate.Click(inputFluid.worldBound.center);
            Assert.IsInstanceOf<NoRecipeSelection>(_selection.Current.Value);
            simulate.Click(inputItem.worldBound.center);
            Assert.AreEqual(new ItemId(1), ((ItemRecipeSelection)_selection.Current.Value).ItemId);
        }

        /// <web-test file="src/features/recipe/views/MachineRecipeEntry.localization.test.ts">機械名をblockGuidから解決し、fallbackと言語変更をアイコンaltと表示・秒数へ反映する</web-test>
        [Test]
        public void 機械名と秒数は言語に追従しアイコンが無ければID文字で描く()
        {
            var guid = RecipeGuid(9);
            var view = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(guid, RecipeLevelInheritance.None)), _context));
            Assert.AreEqual(EnglishBlockName, BlockName(view).text);
            Assert.AreEqual("2.5 seconds", view.Q<Label>($"machine-recipe-box-{guid}-duration").text);

            Localize.TrySetChosenLanguage("japanese");
            simulate.FrameUpdate();
            Assert.AreEqual(JapaneseBlockName, BlockName(view).text);
            Assert.AreEqual("2.5秒", view.Q<Label>($"machine-recipe-box-{guid}-duration").text);

            // 偽のテクスチャ源は絵を返さないのでブロック ID の字で代わりに描く（Web fallback idText）
            // The fake texture source returns no picture, so the block ID text stands in (Web fallback idText)
            var icon = view.Q<GameIcon>(className: "machine-recipe-entry__block-icon");
            Assert.IsTrue(icon.ClassListContains("game-icon--fallback"));
            StringAssert.Contains("1", icon.Q<Label>(className: "game-icon__fallback").text);
        }

        [Test]
        public void 継承バッジは継承するレシピだけに出る()
        {
            var inheriting = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(RecipeGuid(9), Inheriting())), _context));
            var plain = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(RecipeGuid(10), RecipeLevelInheritance.None)), _context));
            Assert.IsNotNull(inheriting.Q<Label>($"machine-recipe-level-inheritance-{RecipeGuid(9)}"));
            Assert.IsNull(plain.Q<Label>($"machine-recipe-level-inheritance-{RecipeGuid(10)}"));

            // 研究で継承が外れた描き直しではバッジを外す
            // A redraw after the inheritance goes away removes the badge
            ((MachineRecipeEntryView)inheriting).Render(new MachineRecipeEntryViewModel(MachineWithFluids(RecipeGuid(9), RecipeLevelInheritance.None)));
            Assert.IsNull(inheriting.Q<Label>($"machine-recipe-level-inheritance-{RecipeGuid(9)}"));
        }

        [Test]
        public void 機械の情報欄はアイコン名前バッジの間が2px()
        {
            var inheriting = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(RecipeGuid(9), Inheriting())), _context));
            var plain = Mount(new MachineRecipeEntryView(new MachineRecipeEntryViewModel(MachineWithFluids(RecipeGuid(10), RecipeLevelInheritance.None)), _context));
            simulate.FrameUpdate();

            // Web <Stack gap={2}>: アイコン → 名前 → バッジの間はどれも 2px
            // Web <Stack gap={2}>: icon → name → badge are each 2px apart
            // 名前の下の余白はラベルの既定で、0 に戻すのは画面の USS（Task 8 の RecipeViewer.uss）なのでここでは除いて測る
            // The name's bottom margin is the label default, reset by the screen USS (Task 8 RecipeViewer.uss), so it is excluded here
            var icon = inheriting.Q(className: "machine-recipe-entry__block-icon");
            var badge = inheriting.Q(className: "level-inheritance-badge");
            var name = BlockName(inheriting);
            Assert.AreEqual(2f, name.worldBound.yMin - icon.worldBound.yMax, 0.01f);
            Assert.AreEqual(2f, badge.worldBound.yMin - (name.worldBound.yMax + name.resolvedStyle.marginBottom), 0.01f);

            // 継承しなければバッジは display:none で、欄は間隔とバッジの高さの分だけ低い
            // Without inheritance the badge is display:none and the column is shorter by the gap plus the badge height
            var plainBadge = plain.Q(className: "level-inheritance-badge");
            Assert.AreEqual(DisplayStyle.None, plainBadge.resolvedStyle.display);
            var inheritingInfo = inheriting.Q(className: "machine-recipe-entry__info").worldBound.height;
            var plainInfo = plain.Q(className: "machine-recipe-entry__info").worldBound.height;
            Assert.Greater(badge.worldBound.height, 0f);
            Assert.AreEqual(2f + badge.worldBound.height, inheritingInfo - plainInfo, 0.01f);
        }

        private static Label BlockName(VisualElement view)
        {
            return view.Q<Label>(className: "machine-recipe-entry__block-name");
        }

        private static RecipeLevelInheritance Inheriting()
        {
            return RecipeLevelInheritance.Inherit(0, Ids(1), 0, Ids(2), 3);
        }

        // 機械 1・2.5 秒・アイテム 1 → 2、液体 1 を 10 入れて 5 出す
        // Machine 1, 2.5 seconds, item 1 → 2, fluid 1 with 10 in and 5 out
        private static MachineRecipeData MachineWithFluids(Guid guid, RecipeLevelInheritance inheritance)
        {
            return new MachineRecipeData(guid, new BlockId(1), 2.5, new[] { Amount(1, 1) }, new[] { Amount(2, 1) },
                new[] { new RecipeFluidAmount(new FluidId(1), 10) }, new[] { new RecipeFluidAmount(new FluidId(1), 5) }, inheritance);
        }
    }
}
