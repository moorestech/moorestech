using System;
using Client.Game.InGame.UI.Inventory.Main;

namespace Client.WebUiHost.Game.Actions
{
    /// <summary>
    /// 検証つき移動の拒否理由を Web の inventory.move_item の失敗コードへ写す（Web ホストの写しはここ 1 か所）
    /// Maps validated-move deny reasons to Web inventory.move_item error codes (the Web host's single mapping)
    /// </summary>
    public static class LocalItemMoveDenyWireCode
    {
        public static string From(LocalItemMoveDenyReason reason)
        {
            return reason switch
            {
                LocalItemMoveDenyReason.SourceEmpty => "empty_slot",
                LocalItemMoveDenyReason.SourceCountShort => "insufficient_count",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
            };
        }
    }
}
