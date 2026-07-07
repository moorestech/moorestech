using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電線ツールのプロトコル送信と延長応答で設置された電柱の受け渡しを担う
    /// Sends electric-wire tool protocols and hands off the pole placed by an extend response
    /// </summary>
    public static class ElectricWireExtendRequestSender
    {
        public static void Connect(Vector3Int fromPos, Vector3Int toPos, ItemId wireItemId)
        {
            ClientContext.VanillaApi.SendOnly.ConnectElectricWire(fromPos, toPos, wireItemId);
        }

        public static void Disconnect(Vector3Int posA, Vector3Int posB)
        {
            ClientContext.VanillaApi.SendOnly.DisconnectElectricWire(posA, posB);
        }

        // 延長応答で解決した電柱の受け渡しスロット。上位がTryConsumePlacedPoleでポーリング取り込みする
        // Hand-off slot for the pole resolved from the extend response; the owner polls it via TryConsumePlacedPole
        private static BlockGameObject _placedPole;
        private static int _placedPoleEpoch;

        /// <summary>
        /// 延長応答で設置された電柱を取り込む。世代一致時のみ有効で、呼び出しでスロットは空になる
        /// Consume the pole placed by an extend response; valid only when the epoch matches, and the slot is cleared on call
        /// </summary>
        public static bool TryConsumePlacedPole(int currentEpoch, out BlockGameObject placedPole)
        {
            placedPole = _placedPole;
            _placedPole = null;
            return placedPole != null && _placedPoleEpoch == currentEpoch;
        }

        /// <summary>
        /// 電柱を設置しつつ起点へ接続し、設置された電柱GameObjectを受け渡しスロットへ保持する
        /// Place a pole while wiring it to the origin, then hold the resolved pole GameObject in the hand-off slot
        /// </summary>
        public static void Extend(Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, ItemId wireItemId, BlockGameObjectDataStore blockDataStore, int epoch)
        {
            UniTask.Create(async () =>
            {
                var response = await ClientContext.VanillaApi.Response.ExtendElectricWire(fromPos, poleBlockId, polePlaceInfo, wireItemId, CancellationToken.None);
                if (response == null || !response.IsSuccess) return;

                // 設置イベントの反映を待ってから電柱GameObjectを解決する
                // Wait for the placement event to apply, then resolve the pole GameObject
                var placedId = new BlockInstanceId(response.PlacedBlockInstanceId);
                await UniTask.WhenAny(
                    UniTask.WaitForSeconds(1f),
                    UniTask.WaitUntil(() => blockDataStore.TryGetBlockGameObject(placedId, out _)));

                if (blockDataStore.TryGetBlockGameObject(placedId, out var placedBlock))
                {
                    _placedPole = placedBlock;
                    _placedPoleEpoch = epoch;
                }
            });
        }
    }
}
