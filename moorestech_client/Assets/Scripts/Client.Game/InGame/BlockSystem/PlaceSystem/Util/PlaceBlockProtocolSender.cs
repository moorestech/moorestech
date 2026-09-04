using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Context;
using Client.Game.InGame.Control;
using Client.Game.InGame.SoundEffect;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    ///     設置確定をサーバーへ送り、Undo履歴と効果音まで面倒を見る
    ///     Sends a confirmed placement to the server, and handles the undo history and sound
    /// </summary>
    public static class PlaceBlockProtocolSender
    {
        // 空バッチは送らないという不変条件を送信本体が持つ。戻り値は送信したか
        // The "never send an empty batch" invariant lives here in the sender; returns whether it sent
        public static bool SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)
        {
            if (currentPlaceInfos.Count == 0) return false;

            // PlaceInfoをサーバー送信
            // Send PlaceInfo to server
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos);

            // Ctrl+Z用に空でない設置バッチを記録
            // Record a non-empty place batch into the undo history for Ctrl+Z
            var record = PlaceOperationRecord.CreateFrom(currentPlaceInfos);
            if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);

            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            return true;
        }

        // 左クリック解放時の設置送信。戻り値は送信したか
        // Sends the placement on left-click release; returns whether it sent
        public static bool TrySendOnClickRelease(List<PlaceInfo> currentPlaceInfos, bool wirePlaceable)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi() || !wirePlaceable) return false;

            // 設置可能セルのみ送信
            // Send only placeable cells
            var placeableInfos = currentPlaceInfos.Where(info => info.Placeable).ToList();

            return SendPlaceBlockProtocol(placeableInfos);
        }
    }
}
