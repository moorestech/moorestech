using System;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using UnityEngine;

namespace Game.Block.Blocks.RocketLaunchPad.Save
{
    internal static class RocketLaunchPadRetainedItemValidator
    {
        internal static void Validate(RocketLaunchPadSaveJsonObject save, RocketLaunchPadSlotLayout layout)
        {
            // 実在品の置き場を復元前に検査
            // Validate storage for existing items before restoring any inventory
            foreach (var input in save.InputItems)
                if (IsPhysical(input, out var id) && (!layout.TryGetInputSlot(id, out _) || !RocketLaunchPadSaveConverter.CanCreate(input)))
                    Fail("inputItems", input);
            for (var i = 0; i < save.OutputItems.Count; i++)
            {
                var output = save.OutputItems[i];
                if (IsPhysical(output, out _) && (layout.OutputSlotCount <= i || !RocketLaunchPadSaveConverter.CanCreate(output)))
                    Fail($"outputItems[{i}]", output);
            }

            // 破損段階の補正と実在積荷を分ける
            // Separate corrupt-stage recovery from retaining existing loaded cargo
            var holdsCargo = save.Phase.HasValue && RocketLaunchPadSaveConverter.HoldsCargo((RocketLaunchPadPhase)save.Phase.Value);
            if (!holdsCargo || save.CargoPayload == null || save.CargoMaterials == null || save.CargoMaterials.Any(item => item == null))
                return; // silent-ok: 構造破損は既存の段階復元が補正 / RestoreStageが理由を警告する
            if (IsPhysical(save.CargoPayload, out var payloadId) &&
                (!layout.TryGetPayloadRequirement(payloadId, out var requirement) || save.CargoPayload.Count != requirement.Count || !RocketLaunchPadSaveConverter.CanCreate(save.CargoPayload)))
                Fail("cargoPayload", save.CargoPayload);
            foreach (var material in save.CargoMaterials)
                if (IsPhysical(material, out _) && !RocketLaunchPadSaveConverter.CanCreate(material)) Fail("cargoMaterials", material);
        }

        private static bool IsPhysical(ItemStackSaveJsonObject item, out ItemId id)
        {
            id = ItemMaster.EmptyItemId;
            if (item.Count <= 0 || !Guid.TryParse(item.ItemGuidStr, out var guid)) return false;
            var known = MasterHolder.ItemMaster.GetItemIdOrNull(guid);
            if (!known.HasValue) return false;
            id = known.Value;
            return true;
        }

        private static void Fail(string field, ItemStackSaveJsonObject item)
        {
            var message = $"発射台の実在品を現在のマスタで保持できないためロードを拒否します field:{field} item:{item.ItemGuidStr} count:{item.Count}";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }
    }
}
