using System.Collections.Generic;
using Client.Game.InGame.Control;
using Server.Protocol.PacketResponse;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 通常設置の左クリック解放時に、UI上か電線不足かを見て設置プロトコルを送る
    /// Sends the placement protocol on left-click release for normal placement, unless the pointer is over UI or wire is short
    /// </summary>
    public static class CommonBlockPlaceSender
    {
        // 送信できたときだけtrue（呼び出し側は自動接続の評価キャッシュを破棄する）
        // True only when actually sent (the caller then drops its auto-connect evaluation cache)
        public static bool TrySendOnClickRelease(List<PlaceInfo> currentPlaceInfos, bool wirePlaceable)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi() || !wirePlaceable) return false;

            SendPlaceBlockProtocol(currentPlaceInfos);
            return true;
        }
    }
}
