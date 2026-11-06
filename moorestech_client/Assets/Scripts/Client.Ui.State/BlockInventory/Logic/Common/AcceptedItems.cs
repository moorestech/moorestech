using System.Collections.Generic;
using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Common
{
    /// <summary>
    /// ホストが配信する受け入れ一覧（上位レベルの変種を含む）の照合
    /// Matching against a host-published acceptance list (higher-level variants included)
    /// </summary>
    public static class AcceptedItems
    {
        public static bool Contains(IReadOnlyList<ItemId> acceptedItemIds, ItemId itemId)
        {
            foreach (var accepted in acceptedItemIds)
            {
                if (accepted.Equals(itemId)) return true;
            }
            return false;
        }
    }
}
