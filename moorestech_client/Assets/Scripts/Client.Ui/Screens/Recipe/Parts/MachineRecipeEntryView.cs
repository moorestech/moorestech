using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Recipe.Logic;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 機械の 1 件（Web MachineRecipeEntry の写し）。矢印は止めたまま、アイテムの後ろに液体を量付きで並べる
    /// One machine entry (port of the Web MachineRecipeEntry); the arrow stays idle and fluids follow items with amounts
    /// </summary>
    public sealed class MachineRecipeEntryView : VisualElement
    {
        private readonly RecipeEntryPartsContext _context;
        private readonly LevelInheritanceBadge _badge;

        public MachineRecipeEntryView(MachineRecipeEntryViewModel first, RecipeEntryPartsContext context)
        {
            _context = context;
            var recipe = first.Recipe;
            var guid = recipe.RecipeGuid;
            name = $"machine-recipe-entry-{guid}";

            // 行は進捗を持たず、秒数は機械レシピの所要時間（Web arrowValue null・duration recipe.time）
            // The row carries no progress and shows the recipe time (Web arrowValue null, duration recipe.time)
            var row = new RecipeRow($"machine-recipe-box-{guid}", $"machine-progress-arrow-{guid}", RecipeDurationText.Create(recipe.Time));
            row.SetIdle();
            // 枠は機械の選択行と共有の組み立てで作り、アイテムだけ押すと選ぶ（Web onLeftDown はアイテムだけ）
            // Slots come from the builder shared with the machine selection row; only items select on press (Web onLeftDown is on items only)
            var materials = MachineRecipeRowContentBuilder.Materials(name, recipe, context.Content);
            var results = MachineRecipeRowContentBuilder.Results(name, recipe, context.Content);
            row.SetSlots(materials.Elements, results.Elements);
            SubscribeItemPresses(materials);
            SubscribeItemPresses(results);

            // 操作の欄は機械のアイコン・名前・継承バッジの縦積み（Web <Stack gap={2}>）
            // The action column stacks the machine icon, name and inheritance badge (Web <Stack gap={2}>)
            var info = new UiStack(UiGap.Px2);
            info.AddToClassList("machine-recipe-entry__info");
            var blockIcon = context.Content.Block(recipe.BlockId);
            var icon = new GameIcon();
            icon.AddToClassList("machine-recipe-entry__block-icon");
            icon.SetIcon(blockIcon, GameIconFallback.IdText);
            var blockName = new LocalizedLabel();
            blockName.AddToClassList("machine-recipe-entry__block-name");
            blockName.SetText(blockIcon.Label);
            _badge = new LevelInheritanceBadge($"machine-recipe-level-inheritance-{guid}");
            info.AddItem(icon);
            info.AddItem(blockName);
            info.AddItem(_badge);
            row.SetAction(info);
            Add(row);
            Render(first);

            #region Internal

            void SubscribeItemPresses(MachineRecipeSideSlots side)
            {
                foreach (var item in side.Items)
                {
                    var itemId = item.ItemId;
                    item.Slot.OnSlotGestured.Subscribe(gesture => OnItemGestured(itemId, gesture));
                }
            }

            void OnItemGestured(ItemId itemId, SlotGesture gesture)
            {
                // silent-ok: アイテムは左押下だけを受ける（Web は onLeftDown だけ渡す）/ MachineRecipeEntryViewTest の左押下の件が見る
                if (gesture.Kind != SlotGestureKind.LeftDown) return;
                _context.Selection.Select(itemId);
            }

            #endregion
        }

        public void Render(MachineRecipeEntryViewModel vm)
        {
            // 継承の上限は研究で変わるのでバッジだけ当て直す（素材と結果は GUID で決まる）
            // The inheritance cap changes with research, so only the badge is re-applied (slots are fixed by the GUID)
            if (LevelInheritanceBadgeText.TryBuild(vm.Recipe.LevelInheritance, out var text)) _badge.Show(text);
            else _badge.Hide();
        }
    }
}
