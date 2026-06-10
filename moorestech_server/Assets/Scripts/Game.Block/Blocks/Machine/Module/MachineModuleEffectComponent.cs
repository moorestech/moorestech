using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.ModulesModule;

namespace Game.Block.Blocks.Machine.Module
{
    /// <summary>
    ///     機械へのモジュール効果倍率の単一の供給源。プロセス中はスナップショットを保持する
    ///     Single source of module effect multipliers for a machine. Holds a snapshot while processing
    /// </summary>
    public class MachineModuleEffectComponent : IBlockComponent
    {
        // プロセス中はスナップショット、未プロセス時は中立効果を返す
        // Returns the snapshot while processing, otherwise the neutral effect
        public MachineModuleEffect CurrentEffect => _processSnapshot ?? MachineModuleEffect.Neutral;
        public float CurrentPowerMultiplier => CurrentEffect.PowerMultiplier;

        private readonly VanillaMachineModuleInventory _moduleInventory;
        private MachineModuleEffect _processSnapshot;

        public MachineModuleEffectComponent(VanillaMachineModuleInventory moduleInventory)
        {
            _moduleInventory = moduleInventory;
        }

        public MachineModuleEffect AggregateCurrent()
        {
            BlockException.CheckDestroy(this);

            // モジュールスロットのアイテムをモジュール定義へ解決し、スタック数で加重して集計する（非モジュールや空スロットは無視）
            // Resolve module slot items into module definitions and aggregate weighted by stack count (skip empty or non-module stacks)
            var modules = new List<MachineModuleEffect.EquippedModule>();
            foreach (var stack in _moduleInventory.ModuleSlot)
            {
                if (stack.Id == ItemMaster.EmptyItemId) continue;

                var itemGuid = MasterHolder.ItemMaster.GetItemMaster(stack.Id).ItemGuid;
                var module = MasterHolder.ModuleMaster.GetModuleElementByItemGuidOrNull(itemGuid);
                if (module == null) continue;

                modules.Add(new MachineModuleEffect.EquippedModule(module, stack.Count));
            }

            return MachineModuleEffect.Aggregate(modules);
        }

        public void SetProcessSnapshot(MachineModuleEffect effect)
        {
            BlockException.CheckDestroy(this);
            _processSnapshot = effect;
        }

        public void ClearProcessSnapshot()
        {
            BlockException.CheckDestroy(this);
            _processSnapshot = null;
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
