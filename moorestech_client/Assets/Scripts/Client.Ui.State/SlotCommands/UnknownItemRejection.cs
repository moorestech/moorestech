using Client.Ui.State.Commands;
using Core.Master;
using UnityEngine;

namespace Client.Ui.State.SlotCommands
{
    /// <summary>
    /// 計画器が未知の ID で Shift 配分を断ったとき、開発者ログと拒否（トースト）の両方で知らせる（D-65）
    /// When the planner refuses a Shift allocation for an unknown ID, report it via a developer log and a rejection (toast) (D-65)
    /// </summary>
    public static class UnknownItemRejection
    {
        public static UiCommandResult Reject(string where, ItemId itemId)
        {
            Debug.LogError($"[{where}] unknown ItemId {itemId} reached the slot planner; the allocation is skipped (client and server masters may differ)");
            return UiCommandRejected.Of(UiCommandRejectReason.ItemUnknownInSlot);
        }
    }
}
