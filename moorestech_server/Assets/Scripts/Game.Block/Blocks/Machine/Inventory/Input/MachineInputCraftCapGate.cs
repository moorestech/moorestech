using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine.Inventory.Input
{
    /// <summary>
    ///     上限つき継承レシピの継承元スロットに、製作上限を超えるレベルを入れさせない判定
    ///     Keeps a capped inheritance recipe's source slot from taking a level above the craft cap
    /// </summary>
    internal class MachineInputCraftCapGate
    {
        private readonly ILevelInheritanceCraftCapLookup _craftCapLookup;

        public MachineInputCraftCapGate(ILevelInheritanceCraftCapLookup craftCapLookup)
        {
            _craftCapLookup = craftCapLookup;
        }

        // 継承元スロット以外・上限なしのレシピは常に通す。束縛（IsInputBoundTo）を通った後に呼ぶ
        // Any slot other than the source, and any uncapped recipe, always passes; called after the binding check (IsInputBoundTo)
        public bool IsWithinCraftCap(MachineRecipeMasterElement recipe, int slotIndex, ItemId itemId)
        {
            if (!MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape)) return true;
            if (!shape.HasCraftCap || slotIndex != shape.SourceInputIndex) return true;

            // 束縛を通った継承元スロットの品は継承元の変種なので、レベルは必ず引ける
            // An item that passed the source slot binding is a source variant, so its level always resolves
            var level = shape.ResolveSourceLevel(itemId);
            return level.HasValue && level.Value <= MachineRecipeLevelInheritanceUtil.GetMaxCraftableLevel(shape, _craftCapLookup);
        }
    }
}
