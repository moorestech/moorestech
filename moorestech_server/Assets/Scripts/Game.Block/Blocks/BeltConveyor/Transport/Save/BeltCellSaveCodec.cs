using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    internal static class BeltCellSaveCodec
    {
        internal static BeltCellSaveState Capture(VanillaBeltConveyorComponent component)
        {
            var result = new BeltCellSaveState { PriorityOrder = component.PriorityOrder, Items = new List<BeltCellSavedItem>() };
            foreach (var state in component.CaptureItems())
            {
                var item = new BeltCellSavedItem { ItemStack = new ItemStackSaveJsonObject(component.GetStack(state.Item.Guid)),
                    InstanceId = BeltTransportIdentity.ToItemInstanceId(state.Item.Guid).AsPrimitive(), Progress = state.Progress,
                    EntryDirection = (int)state.EntryDirection, EntryHeight = state.EntryHeight };
                if (state.IsBuffer) result.BufferItem = item;
                else result.Items.Add(item);
            }
            return result;
        }

        internal static void Load(VanillaBeltConveyorComponent component, object value, double transitSeconds)
        {
            var state = value as JObject ?? JObject.FromObject(value);
            // 旧秒数の変換はマスタが利用できるロード境界で行う。
            // Resolve legacy seconds at the load boundary where master data exists.
            if (state["legacyItems"] is JArray legacy)
            {
                for (int index = 0; index < legacy.Count; index++)
                {
                    if (legacy[index].Type == JTokenType.Null) continue;
                    var old = legacy[index].Type == JTokenType.String ? JObject.Parse((string)legacy[index]) : (JObject)legacy[index];
                    if (old["itemStack"] == null || old["itemStack"].Type == JTokenType.Null) continue;
                    var itemStack = old["itemStack"].ToObject<ItemStackSaveJsonObject>();
                    int progress = Math.Max(1, Math.Min(256, (int)Math.Round(256 * (1 - (double)old["remainingSeconds"] / transitSeconds))));
                    var sourceGuid = old["sourceConnectorGuid"]?.ToObject<Guid?>();
                    var direction = component.FindInputDirection(sourceGuid);
                    long instanceId = ((long)component.CellId << 32) | (uint)(index + 1);
                    Add(new BeltCellSavedItem { ItemStack = itemStack, InstanceId = instanceId, Progress = progress,
                        EntryDirection = (int)direction, EntryHeight = 0 }, false);
                }
                component.SetLoadedPriority(-1);
                return;
            }
            var saved = state.ToObject<BeltCellSaveState>();
            component.SetLoadedPriority(saved.PriorityOrder);
            foreach (var item in saved.Items) Add(item, false);
            if (saved.BufferItem != null) Add(saved.BufferItem, true);

            #region Internal
            void Add(BeltCellSavedItem savedItem, bool buffer)
            {
                // マスタ欠損除去で空にされた保存stackを復活させない。
                // Do not recreate saved stacks emptied by missing-master pruning.
                if (savedItem.ItemStack.Count == 0)
                {
                    Debug.LogWarning($"Skipped pruned belt item: cell={component.CellId}, instance={savedItem.InstanceId}");
                    return;
                }
                if (savedItem.ItemStack.Count != 1 || savedItem.Progress < 1 || savedItem.Progress > 256 ||
                    savedItem.EntryDirection < 0 || savedItem.EntryDirection > 3 || Math.Abs(savedItem.EntryHeight) > 1)
                    throw new ArgumentException($"Invalid belt item state: cell={component.CellId}, instance={savedItem.InstanceId}");
                var id = MasterHolder.ItemMaster.GetItemId(savedItem.ItemStack.ItemGuid);
                var stack = ServerContext.ItemStackFactory.Create(id, 1, new ItemInstanceId(savedItem.InstanceId));
                var item = new BeltItem(BeltTransportIdentity.ToGuid(stack.ItemInstanceId), id.AsPrimitive());
                component.AddPending(new BeltCellItemState(component.CellId, savedItem.Progress,
                    (BeltDirection)savedItem.EntryDirection, savedItem.EntryHeight, item, buffer), stack);
            }
            #endregion
        }
    }
}
