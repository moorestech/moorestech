using System;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.State.Recipe;
using Client.Ui.Components.Gauges;
using Client.Ui.Content;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Toast;
using Core.Master;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Recipe
{
    /// <summary>
    /// レシピ画面 L1 の共通の材料（テスト用 mod のマスタ・依存の束・クラフトの VM・矢印の進み）
    /// Shared material for recipe-screen L1 tests (test-mod master, dependency bundle, craft VMs, arrow progress)
    /// </summary>
    public static class RecipeScreenFixtures
    {
        public static RecipeEntryPartsContext Context(UiCommandResultToaster toaster, RecordingRecipeCommands commands, RecipeItemSelection selection)
        {
            // 名前は ID からマスタで引くのでテスト用 mod を読む。絵は無くても ID の字で代わりに出る
            // Names resolve through the master, so load the test mod; without a texture the ID text stands in
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var content = new GameContentPresentation(new FakeContentTextureSource(null, null, null));
            return new RecipeEntryPartsContext(content, commands, toaster, selection);
        }

        public static RecipeViewerItemSelected Selected(int itemId, params RecipeEntryViewModel[] entries)
        {
            return new RecipeViewerItemSelected(new ItemId(itemId), entries);
        }

        // 先頭だけアンカーを持つ作れるクラフト n 件（GUID は 1..n）
        // n craftable crafts with the anchor on the head only (GUIDs 1..n)
        public static RecipeEntryViewModel[] ManyCrafts(int count)
        {
            var entries = new RecipeEntryViewModel[count];
            for (var i = 0; i < count; i++) entries[i] = CraftVm(RecipeFixtures.RecipeGuid(i + 1), 1, true, i == 0, Material(1, 1, 9));
            return entries;
        }

        public static CraftRecipeEntryViewModel CraftVm(Guid guid, double craftTime, bool craftable, bool anchor, params RecipeMaterialViewModel[] materials)
        {
            var required = new ItemAmount[materials.Length];
            for (var i = 0; i < materials.Length; i++) required[i] = new ItemAmount(materials[i].ItemId, materials[i].Required);
            return new CraftRecipeEntryViewModel(RecipeFixtures.Craft(guid, 7, 1, craftTime, required), materials, craftable, anchor);
        }

        public static RecipeMaterialViewModel Material(int itemId, int required, int owned)
        {
            return new RecipeMaterialViewModel(new ItemId(itemId), required, owned, owned < required);
        }

        // 矢印の充填の切り抜き幅（viewBox 幅に対する %）を進み 0..1 へ戻す
        // Convert the arrow fill clip width (percent of the viewBox width) back to a 0..1 progress
        public static float ArrowProgress(VisualElement view, Guid guid)
        {
            var clip = view.Q<ProgressArrowGlyph>($"craft-progress-arrow-{guid}").Q(className: "progress-arrow-glyph__fill-clip");
            return clip.style.width.value.value / (ProgressArrowGlyphPath.ArrowSpan / ProgressArrowGlyphPath.ViewBoxWidth * 100f);
        }
    }
}
