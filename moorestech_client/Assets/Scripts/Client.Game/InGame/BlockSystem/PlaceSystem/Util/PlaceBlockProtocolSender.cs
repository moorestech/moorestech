using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
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
        // 新規設置バッチの送信。Ctrl+Z用の記録は設置レコード
        // Sends a new-placement batch, recording it for Ctrl+Z as a place record
        public static bool SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)
        {
            return SendPlaceBlockProtocol(currentPlaceInfos, PlaceOperationRecord.CreateFrom(currentPlaceInfos));
        }

        // 空バッチは送らないという不変条件を送信本体が持つ。戻り値は送信したか
        // The "never send an empty batch" invariant lives here in the sender; returns whether it sent
        private static bool SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos, IBuildOperationRecord undoRecord)
        {
            if (currentPlaceInfos.Count == 0) return false;

            // PlaceInfoをサーバー送信
            // Send PlaceInfo to server
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos);

            // Ctrl+Z用に空でないバッチを記録
            // Record a non-empty batch into the undo history for Ctrl+Z
            if (undoRecord.HasCells) ClientDIContext.BuildOperationHistory.Push(undoRecord);

            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
            return true;
        }

        // 左クリック解放時の設置送信。戻り値は送信したか
        // Sends the placement on left-click release; returns whether it sent
        public static bool TrySendOnClickRelease(List<PlaceInfo> currentPlaceInfos, bool wirePlaceable)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi() || !wirePlaceable) return false;

            return SendPlaceBlockProtocol(SelectPlaceableCells(currentPlaceInfos));
        }

        // 張替え列の左クリック解放時の送信。戻り値は送信したか
        // Sends the replace run on left-click release; returns whether it sent
        public static bool TrySendReplaceOnClickRelease(List<PlaceInfo> currentPlaceInfos, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            if (UiPointerHitTest.IsPointerOverAnyUi()) return false;

            var placeableInfos = SelectPlaceableCells(currentPlaceInfos);

            // 逆張替えレコードは既設IDが残っている送信前に作る
            // The reverse-replace record is built before sending, while the existing ids are still there
            return SendPlaceBlockProtocol(placeableInfos, ReplaceOperationRecord.CreateFrom(placeableInfos, blockGameObjectDataStore));
        }

        // 送信対象を設置可能セルへ絞る唯一の定義
        // The single definition narrowing what gets sent down to the placeable cells
        private static List<PlaceInfo> SelectPlaceableCells(List<PlaceInfo> currentPlaceInfos)
        {
            return currentPlaceInfos.Where(info => info.Placeable).ToList();
        }
    }
}
