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
        public static void SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)
        {
            // セル毎BlockId付きでPlaceInfoをサーバーに送信
            // Send PlaceInfo to server; each cell already carries its own BlockId
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos);

            // Ctrl+Z用に設置バッチを履歴へ記録する（全セル設置不能の空バッチは積まない）
            // Record the place batch into the undo history for Ctrl+Z (skip empty batches where no cell was placeable)
            var record = PlaceOperationRecord.CreateFrom(currentPlaceInfos);
            if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);

            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
        }

        // 左クリック解放時の設置送信。戻り値は送信したか
        // Sends the placement on left-click release; returns whether it sent
        public static bool TrySendOnClickRelease(List<PlaceInfo> currentPlaceInfos, bool wirePlaceable)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi() || !wirePlaceable) return false;

            // 設置可能セルだけを送る（不可セルはサーバーでも拒否されるため送らない）
            // Send only placeable cells; blocked cells would be rejected by the server anyway
            var placeableInfos = currentPlaceInfos.Where(info => info.Placeable).ToList();

            // 1セルも置けないなら空パケットも設置音も出さない（鉱脈外の採掘機クリックが毎回音を鳴らすのを防ぐ）
            // With no placeable cell, send no empty packet and play no sound (an off-vein miner click would otherwise sound every time)
            if (placeableInfos.Count == 0) return false;

            SendPlaceBlockProtocol(placeableInfos);
            return true;
        }
    }
}
