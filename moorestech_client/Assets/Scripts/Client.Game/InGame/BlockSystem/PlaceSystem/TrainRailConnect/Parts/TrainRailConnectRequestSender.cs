using System;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Train.RailGraph;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect.Parts
{
    /// <summary>
    /// 橋脚設置つきレール接続プロトコルの送信と、応答で確定した引き継ぎ先接続エリアの保持。
    /// コールバックは持たず、結果は上位がTryConsumePlacedPierAreaでループ先頭から取り込む一方向構造。
    /// Sends the place-pier-and-connect protocol and holds the resolved next-origin connect area from the response.
    /// No callbacks: the upper layer consumes the result via TryConsumePlacedPierArea at the top of its loop, keeping the flow one-way.
    /// </summary>
    public class TrainRailConnectRequestSender
    {
        private readonly RailGraphClientCache _cache;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;

        // 応答待ち中に無効化や再送信が起きても、最後の1件以外の古い応答を確実に捨てるための世代トークン。
        // Sendは自分の世代番号をキャプチャし、応答到着時に現在値と一致した場合のみ結果を反映する。
        // Generation token that guarantees only the latest response survives invalidation or re-sending while awaiting.
        // Each Send captures its own generation and applies the result only when it still matches on arrival.
        private int _generation;

        private IRailComponentConnectAreaCollider _resolvedArea;

        public TrainRailConnectRequestSender(RailGraphClientCache cache, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _cache = cache;
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        /// <summary>
        /// 進行中の応答と未取り込みの結果を無効化する（有効化・無効化・接続元再選択時に呼ぶ）
        /// Invalidate pending responses and any unconsumed result (call on enable, disable or origin re-selection)
        /// </summary>
        public void Invalidate()
        {
            _generation++;
            _resolvedArea = null;
        }

        /// <summary>
        /// 応答で確定した引き継ぎ先の接続エリアを一度だけ取り出す
        /// Consume the resolved next-origin connect area from the response exactly once
        /// </summary>
        public bool TryConsumePlacedPierArea(out IRailComponentConnectAreaCollider placedArea)
        {
            placedArea = _resolvedArea;
            _resolvedArea = null;
            return placedArea != null;
        }

        /// <summary>
        /// 橋脚を設置しつつレールを接続する。成功時の引き継ぎ先はTryConsumePlacedPierAreaで取り込む
        /// Place a pier and connect the rail; consume the resulting next origin via TryConsumePlacedPierArea
        /// </summary>
        public void SendPlacePierAndConnect(IRailNode fromNode, BlockId pierBlockId, PlaceInfo pierPlaceInfo, Guid railTypeGuid)
        {
            var generation = ++_generation;
            _resolvedArea = null;

            UniTask.Create(async () =>
            {
                var response = await ClientContext.VanillaApi.Response.PlaceRailWithPier(fromNode.NodeId, fromNode.NodeGuid, pierBlockId, pierPlaceInfo, railTypeGuid, CancellationToken.None);
                if (!response.Success) return;

                // 設置済み橋脚の接続エリアが解決できたときだけ引き継ぎ先として残す
                // Keep the next origin only when the placed pier's connect area could be resolved
                var placedArea = await PlacedPierConnectAreaResolver.Resolve(_cache, _blockGameObjectDataStore, response.ToNodeId);

                // 世代が進んでいたら応答ごと破棄する。持ち替えや再選択の後に古い橋脚が接続元へ戻るのを防ぐ
                // Discard everything when the generation has advanced, so a stale pier never returns as the origin after a tool switch or re-selection
                if (generation != _generation) return;
                _resolvedArea = placedArea;
            });
        }
    }
}
