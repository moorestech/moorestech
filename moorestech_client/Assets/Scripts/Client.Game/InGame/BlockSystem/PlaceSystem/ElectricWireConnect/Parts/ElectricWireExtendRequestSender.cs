using System;
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
    /// 電線延長プロトコルの送信と、応答で確定した結果の保持。
    /// コールバックは持たず、結果は上位がTryConsumeOutcomeでループ先頭から取り込む一方向構造。
    /// Sends the electric wire extend protocol and holds the settled outcome from the response.
    /// No callbacks: the upper layer consumes the result via TryConsumeOutcome at the top of its loop.
    /// </summary>
    public class ElectricWireExtendRequestSender
    {
        // 新規電柱のエンティティ生成を待つ上限秒数
        // Timeout seconds for waiting the new pole entity to spawn
        private const float EndpointSpawnWaitSeconds = 1f;

        private readonly BlockGameObjectDataStore _blockDataStore;

        // 応答待ち中の無効化・再送信で古い応答を捨てるための世代トークン
        // Generation token that discards stale responses across invalidation or re-sending
        private int _generation;
        private bool _hasOutcome;
        private ElectricWireExtendOutcome _outcome;

        public bool IsAwaitingResponse { get; private set; }

        public ElectricWireExtendRequestSender(BlockGameObjectDataStore blockDataStore)
        {
            _blockDataStore = blockDataStore;
        }

        /// <summary>
        /// 進行中の応答と未取り込みの結果を無効化する（ツール無効化・起点解除時に呼ぶ）
        /// Invalidate pending responses and any unconsumed result (call on tool disable or origin release)
        /// </summary>
        public void Invalidate()
        {
            _generation++;
            IsAwaitingResponse = false;
            ClearOutcome();
        }

        /// <summary>
        /// 応答で確定した結果を一度だけ取り出す
        /// Consume the settled outcome from the response exactly once
        /// </summary>
        public bool TryConsumeOutcome(out ElectricWireExtendOutcome outcome)
        {
            outcome = _outcome;
            var hadOutcome = _hasOutcome;
            ClearOutcome();
            return hadOutcome;
        }

        public void SendConnect(Vector3Int fromPos, Vector3Int toPos, Guid connectToolGuid)
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            Send(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateConnectRequest(playerId, fromPos, toPos, connectToolGuid));
        }

        public void SendExtend(Vector3Int fromPos, BlockId poleBlockId, PlaceInfo polePlaceInfo, Guid connectToolGuid)
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            Send(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateExtendRequest(playerId, fromPos, poleBlockId, polePlaceInfo, connectToolGuid));
        }

        public void SendIsolatedPlace(BlockId poleBlockId, PlaceInfo polePlaceInfo)
        {
            var playerId = ClientContext.PlayerConnectionSetting.PlayerId;
            Send(ElectricWireExtendProtocol.ElectricWireExtendRequest.CreateIsolatedPlaceRequest(playerId, poleBlockId, polePlaceInfo));
        }

        public void Disconnect(Vector3Int posA, Vector3Int posB)
        {
            ClientContext.VanillaApi.SendOnly.DisconnectElectricWire(posA, posB);
        }

        private void Send(ElectricWireExtendProtocol.ElectricWireExtendRequest request)
        {
            var generation = ++_generation;
            IsAwaitingResponse = true;
            ClearOutcome();

            UniTask.Create(async () =>
            {
                var isSuccess = false;
                BlockGameObject endpoint = null;

                // ネットワーク送受信は外部境界。例外が漏れると応答待ちが解除されず、以降クリックを一切受け付けなくなるため隔離する
                // Network I/O is an external boundary; an escaping exception would strand the awaiting flag and reject every later click
                try
                {
                    // 応答を待ち、成功時のみ終点ブロックの生成を待って次起点を解決する
                    // Await the response, then resolve the next origin only on success
                    var response = await ClientContext.VanillaApi.Response.SendElectricWireExtend(request, CancellationToken.None);
                    isSuccess = response is { IsSuccess: true };
                    if (isSuccess) endpoint = await WaitForEndpoint(new BlockInstanceId(response.EndpointBlockInstanceId));
                }
                finally
                {
                    // 世代が進んでいたら破棄済みの結果として捨てる
                    // Discard the result when the generation has advanced
                    if (generation == _generation)
                    {
                        IsAwaitingResponse = false;
                        _hasOutcome = true;
                        _outcome = new ElectricWireExtendOutcome(isSuccess, endpoint);
                    }
                }
            });
        }

        private void ClearOutcome()
        {
            _hasOutcome = false;
            _outcome = default;
        }

        private async UniTask<BlockGameObject> WaitForEndpoint(BlockInstanceId endpointId)
        {
            // エンティティ生成をタイムアウト付きで毎フレーム確認する（前例: GearChainPoleExtendRequestSender.WaitForPlacedPole）
            // Poll the entity spawn every frame with a timeout (precedent: GearChainPoleExtendRequestSender.WaitForPlacedPole)
            var startTime = Time.time;
            while (Time.time - startTime < EndpointSpawnWaitSeconds)
            {
                if (_blockDataStore.TryGetBlockGameObject(endpointId, out var endpointBlock)) return endpointBlock;
                await UniTask.NextFrame();
            }

            return null;
        }
    }
}
