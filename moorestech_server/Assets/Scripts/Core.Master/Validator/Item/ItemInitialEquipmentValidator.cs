using System;
using System.Collections.Generic;
using Mooresmaster.Model.ItemsModule;

namespace Core.Master.Validator.Item
{
    // 初期装備は未定義アイテムを指せない。装備スロットを超える分は投入時に捨てられるためここで弾く
    // Initial equipment must reference defined items; entries beyond the equipment slot count would be dropped on grant, so reject them here
    internal static class ItemInitialEquipmentValidator
    {
        internal static string Validate(Items items, HashSet<Guid> allItemGuids)
        {
            var logs = "";

            // 初期レベルの最大スタック数はItemStack生成が生の例外で落ちるため、ここで先に弾く
            // ItemStack creation throws raw past the level-1 max stack, so it is rejected here first

            foreach (var initial in items.InitialEquipmentItems)
            {
                if (!allItemGuids.Contains(initial.ItemGuid))
                {
                    logs += $"[ItemMaster.initialEquipmentItems] has invalid ItemGuid:{initial.ItemGuid}\n";
                    continue;
                }
                if (initial.ItemCount <= 0)
                    logs += $"[ItemMaster.initialEquipmentItems] ItemGuid:{initial.ItemGuid} has non-positive ItemCount:{initial.ItemCount}\n";
                logs += InitialLevelMaxStackValidation(initial.ItemGuid, initial.ItemCount);
            }
            if (items.EquipmentSlotCount < items.InitialEquipmentItems.Length)
                logs += $"[ItemMaster.initialEquipmentItems] count:{items.InitialEquipmentItems.Length} exceeds EquipmentSlotCount:{items.EquipmentSlotCount}\n";
            // 装備選択は「未装備なし」で常に1枠を指すため、枠0は選択インデックスのクランプが成立せず起動不能になる
            // Equipment selection always points at a slot (no unequipped state), so zero slots breaks the index clamp and the boot
            if (items.EquipmentSlotCount < 1)
                logs += $"[ItemMaster.equipmentSlotCount] must be at least 1 but was {items.EquipmentSlotCount}\n";

            return logs;

            #region Internal

            // 参照先テーブルが壊れている件は ItemStackLevelTableValidator の担当なので、ここでは黙って見送る
            // A broken table reference is ItemStackLevelTableValidator's job, so it is skipped silently here
            string InitialLevelMaxStackValidation(Guid itemGuid, int itemCount)
            {
                var initialLevelMaxStack = ItemStackLevelTableValidator.GetInitialStackLimit(items, itemGuid);
                // silent-ok: 参照異常は専用検証器が診断する / マスタ読込エラーで開発者が気づく
                if (initialLevelMaxStack == null) return "";
                if (itemCount <= initialLevelMaxStack) return "";
                return $"[ItemMaster.initialEquipmentItems] ItemGuid:{itemGuid} has ItemCount:{itemCount} exceeding initial level MaxStack:{initialLevelMaxStack}\n";
            }

            #endregion
        }
    }
}
