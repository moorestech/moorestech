using Client.Ui.State.Items;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.SlotCommands
{
    /// <summary>
    /// DirectMovePlanner は未知の ItemId で例外になるので、配分の前に元の枠が既知の ID かを確かめる（L0-s4a の申し送り）
    /// DirectMovePlanner throws on unknown ItemIds, so check the source slot holds a known ID before allocating (L0-s4a handoff)
    /// </summary>
    public static class KnownItemSlots
    {
        public static bool IsKnownOrEmpty(SlotItem slot, string where)
        {
            if (slot.IsEmpty || MasterHolder.ItemMaster.ExistItemId(slot.ItemId)) return true;
            Debug.LogError($"[{where}] unknown ItemId {slot.ItemId} reached the slot planner; the allocation is skipped (client and server masters may differ)");
            return false;
        }
    }
}
