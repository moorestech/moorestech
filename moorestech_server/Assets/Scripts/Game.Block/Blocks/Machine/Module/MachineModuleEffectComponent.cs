using System;
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
            return MachineModuleEffect.Aggregate(CollectEffectiveModules());
        }

        // 効いているモジュールを上限で切った個数で集める
        // Collect effective modules with counts clipped to the slot limit
        public List<MachineModuleEffect.EquippedModule> CollectEffectiveModules()
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

                // silent-ok: 配置は CheckPlacement が拒否しログを出すので、ここへ来るのは制限導入前のセーブ由来だけ / プレイヤーは機械UIのモジュール欄でそのモジュールを見て取り出せる
                if (!_moduleInventory.IsModuleEffective(module)) continue;

                // silent-ok: 上限超えは移動・入れ替えで弾くので、ここへ来るのは判定を通さない書き込みだけ / 機械UIのモジュール欄に個数と「1スロット上限 N」が並ぶ（C9）のでプレイヤーが気づく
                var effectiveCount = Math.Min(stack.Count, _moduleInventory.GetSlotStackLimit());
                modules.Add(new MachineModuleEffect.EquippedModule(module, effectiveCount));
            }

            return modules;
        }

        // 改善候補をあと1個、効く形で刺せるか
        // Whether one more candidate module can be equipped effectively
        public bool CanEquipOneMore(ModuleMasterElement module)
        {
            BlockException.CheckDestroy(this);
            return _moduleInventory.CanAcceptOneMore(module);
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
