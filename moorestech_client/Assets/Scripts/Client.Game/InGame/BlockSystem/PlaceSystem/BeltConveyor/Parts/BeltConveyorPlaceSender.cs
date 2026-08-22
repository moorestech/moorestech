using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Control;
using Server.Protocol.PacketResponse;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 左クリック解放時、UI外なら設置可能セルを送信
    /// Sends placeable cells on left-click release, unless over UI
    /// </summary>
    internal static class BeltConveyorPlaceSender
    {
        // true=送信成功。呼び出し側が連続設置状態をリセット
        // True only when actually sent; the caller then resets the continuous-placement state
        public static bool TrySendOnClickRelease(List<PlaceInfo> currentPlaceInfos)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;

            SendPlaceBlockProtocol(currentPlaceInfos.Where(info => info.Placeable).ToList());
            return true;
        }
    }
}
