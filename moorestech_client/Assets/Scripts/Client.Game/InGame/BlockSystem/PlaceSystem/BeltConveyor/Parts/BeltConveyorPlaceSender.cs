using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Control;
using Client.Game.InGame.Player;
using Client.Input;
using Common.Debug;
using Server.Protocol.PacketResponse;
using UnityEngine;
using static Client.Game.InGame.BlockSystem.PlaceSystem.Util.PlaceSystemUtil;
using static Client.Game.DebugConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ベルト設置の距離判定と左クリック解放時の送信処理
    /// Belt placement's distance check and left-click-release send handling
    /// </summary>
    public static class BeltConveyorPlaceSender
    {
        public static bool IsPlaceableDistance(Vector3Int placePoint, float maxDistance)
        {
            var placePosition = (Vector3)placePoint;
            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;

            return Vector3.Distance(playerPosition, placePosition) <= maxDistance;
        }

        // 押下未登録の解放は無視する（ビルドメニュー選択クリックの解放がPlaceBlock遷移直後に漏れて
        // Enableのセンチネル-1をheightOffsetへ書き込み、以後の設置が1段沈むのを防ぐ）
        // Ignore releases without a registered press (the build-menu selection click's release can leak in right
        // after entering PlaceBlock and write Enable's -1 sentinel into heightOffset, sinking later placements)
        public static void HandleClickRelease(List<PlaceInfo> currentPlaceInfos, ref Vector3Int? clickStartPosition, ref int heightOffset, int clickStartHeightOffset)
        {
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

            // デバッグモード時は送信しない
            // Skip sending in debug mode
            if (DebugParameters.GetValueOrDefaultBool(PlacePreviewKeepKey)) return;

            if (!clickStartPosition.HasValue) return;

            // マウスを離したので連続設置状態は解除する（設置有無に関わらず）
            // Clear the continuous-placement state on mouse release (regardless of whether we place)
            heightOffset = clickStartHeightOffset;
            clickStartPosition = null;

            if (UiPointerHitTest.IsPointerOverAnyUi()) return;
            SendPlaceBlockProtocol(currentPlaceInfos.Where(info => info.Placeable).ToList());
        }
    }
}
