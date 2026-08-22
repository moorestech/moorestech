using System.Collections.Generic;
using Client.Game.InGame.Control;
using Server.Protocol.PacketResponse;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 左クリック解放時、UI/電線不足を見て設置送信
    /// Sends the placement protocol on release, unless over UI or wire is short
    /// </summary>
    public static class CommonBlockPlaceSender
    {
        // true=送信成功。呼び出し側が自動接続キャッシュを破棄
        // True only when actually sent; the caller then drops its auto-connect cache
        public static bool TrySendOnClickRelease(List<PlaceInfo> currentPlaceInfos, bool wirePlaceable)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi() || !wirePlaceable) return false;

            SendPlaceBlockProtocol(currentPlaceInfos);
            return true;
        }
    }
}
