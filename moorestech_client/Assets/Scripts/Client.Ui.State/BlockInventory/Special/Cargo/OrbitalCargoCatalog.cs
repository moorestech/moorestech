using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Items;
using Core.Master;
using Game.Block.Interface.Component;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// ブロックが運ぶ種類から、積荷表（マスタ）の 1 便と成果で選択肢を組む
    /// Builds the choices from the kinds a block carries, using the payload table's per-flight count and outcome
    /// </summary>
    public static class OrbitalCargoCatalog
    {
        public static IReadOnlyList<OrbitalCargoChoice> ForCarried(IReadOnlyList<ItemId> carriedPayloadItemIds)
        {
            var choices = new List<OrbitalCargoChoice>(carriedPayloadItemIds.Count);
            foreach (var payloadItemId in carriedPayloadItemIds) choices.Add(Create(payloadItemId));
            return choices;

            #region Internal

            OrbitalCargoChoice Create(ItemId payloadItemId)
            {
                // 運ぶ種類は積荷表から決まるので必ず引ける。引けなければマスタの矛盾として止める
                // Carried kinds come from the payload table, so the lookup succeeds; a miss is a master contradiction and stops here
                var payloadGuid = MasterHolder.ItemMaster.GetItemGuid(payloadItemId);
                var entry = MasterHolder.ItemMaster.Items.OrbitalPayloads.FirstOrDefault(payload => payload.PayloadItemGuid == payloadGuid);
                if (entry == null) throw new InvalidOperationException($"積荷表に無い種類をブロックが運ぶと定義されています payload={payloadGuid}");
                var outputs = entry.ArrivalOutputs.Select(output => new ItemAmount(MasterHolder.ItemMaster.GetItemId(output.ItemGuid), output.Count)).ToList();
                return new OrbitalCargoChoice(payloadItemId, entry.CountPerLaunch, outputs, SpaceElevatorCableItems.Contains(payloadItemId));
            }

            #endregion
        }
    }
}
