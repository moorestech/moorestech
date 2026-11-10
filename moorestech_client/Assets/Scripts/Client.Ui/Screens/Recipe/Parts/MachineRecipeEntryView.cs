using System;
using System.Collections.Generic;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
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
            row.SetSlots(BuildSide(guid, recipe.InputItems, recipe.InputFluids, "input"), BuildSide(guid, recipe.OutputItems, recipe.OutputFluids, "output"));

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
        }

        public void Render(MachineRecipeEntryViewModel vm)
        {
            // 継承の上限は研究で変わるのでバッジだけ当て直す（素材と結果は GUID で決まる）
            // The inheritance cap changes with research, so only the badge is re-applied (slots are fixed by the GUID)
            if (LevelInheritanceBadgeText.TryBuild(vm.Recipe.LevelInheritance, out var text)) _badge.Show(text);
            else _badge.Hide();
        }

        private IReadOnlyList<VisualElement> BuildSide(Guid guid, IReadOnlyList<ItemAmount> items, IReadOnlyList<RecipeFluidAmount> fluids, string side)
        {
            // アイテム（数付き・押すと選ぶ）の後ろに液体（量付き・遷移なし）
            // Items (with counts, select on press) followed by fluids (with amounts, no navigation)
            var slots = new List<VisualElement>(items.Count + fluids.Count);
            for (var i = 0; i < items.Count; i++) slots.Add(CreateItemSlot(items[i], $"machine-recipe-box-{guid}-{side}-{i}"));
            for (var i = 0; i < fluids.Count; i++) slots.Add(CreateFluidSlot(fluids[i], $"{name}-{side}-fluid-{i}"));
            return slots;
        }

        private ItemSlot CreateItemSlot(ItemAmount amount, string testid)
        {
            // 機械レシピは手で作らないので所持は見ず必要数だけ出す（Web count={r.count}）
            // Machine recipes are not hand-crafted, so show only the required count (Web count={r.count})
            var slot = new ItemSlot(testid);
            slot.SetContent(new ItemSlotContent(_context.Content.Item(amount.ItemId), amount.Count, new ItemSlotMarks(false, false, false), null, null, null));
            slot.OnSlotGestured.Subscribe(gesture => OnItemGestured(amount.ItemId, gesture));
            return slot;
        }

        private void OnItemGestured(ItemId itemId, SlotGesture gesture)
        {
            // silent-ok: アイテムは左押下だけを受ける（Web は onLeftDown だけ渡す）/ MachineRecipeEntryViewTest の左押下の件が見る
            if (gesture.Kind != SlotGestureKind.LeftDown) return;
            _context.Selection.Select(itemId);
        }

        private FluidAmountSlot CreateFluidSlot(RecipeFluidAmount fluid, string testid)
        {
            // 液体はレシピ一覧の遷移先を持たないので押下を購読しない（前例 MachineRecipeSelectionRow）
            // Fluids have no recipe-list destination, so presses are not subscribed (precedent: MachineRecipeSelectionRow)
            var slot = new FluidAmountSlot(testid);
            slot.SetFluid(_context.Content.Fluid(fluid.FluidId), SlotAmountBadge.Show(fluid.Amount));
            return slot;
        }
    }
}
