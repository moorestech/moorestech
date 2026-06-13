using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.ItemsModule;

namespace Game.Block.Blocks.Machine.Module
{
    /// <summary>
    ///     装着中モジュールから効果倍率を都度集計する供給源
    ///     Aggregates effect multipliers live from the equipped modules
    /// </summary>
    public class MachineModuleEffectComponent : IBlockComponent
    {
        private readonly VanillaMachineModuleInventory _moduleInventory;

        public MachineModuleEffectComponent(VanillaMachineModuleInventory moduleInventory)
        {
            _moduleInventory = moduleInventory;
        }

        public MachineModuleEffect AggregateCurrent()
        {
            BlockException.CheckDestroy(this);

            // スロットのアイテムをモジュール定義へ解決して集計
            // Resolve slot items to module definitions and aggregate
            var modules = new List<MachineModuleEffect.EquippedModule>();
            foreach (var stack in _moduleInventory.ModuleSlot)
            {
                if (stack.Id == ItemMaster.EmptyItemId) continue;

                var module = MasterHolder.ItemMaster.GetModuleByItemIdOrNull(stack.Id);
                if (module == null) continue;

                modules.Add(new MachineModuleEffect.EquippedModule(module, stack.Count));
            }

            return MachineModuleEffect.Aggregate(modules);
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
