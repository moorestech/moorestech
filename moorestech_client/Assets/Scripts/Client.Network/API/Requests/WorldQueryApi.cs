using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    internal sealed class WorldQueryApi
    {
        private readonly PacketExchangeManager _packetExchangeManager;

        public WorldQueryApi(PacketExchangeManager packetExchangeManager)
        {
            _packetExchangeManager = packetExchangeManager;
        }

        public async UniTask<List<GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new GetMapObjectInfoProtocol.RequestMapObjectInfosMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetMapObjectInfoProtocol.ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }

        // マップレイアウト（spawn/mapObjects/mapVeins）をハンドシェイク時に取得する
        // Fetch the map layout (spawn/mapObjects/mapVeins) during the handshake
        public async UniTask<GetMapDataProtocol.ResponseMapDataMessagePack> GetMapData(CancellationToken ct)
        {
            var request = GetMapDataProtocol.RequestMapDataMessagePack.CreateLayoutRequest();
            return await _packetExchangeManager.GetPacketResponse<GetMapDataProtocol.ResponseMapDataMessagePack>(request, ct);
        }

        // 地形バイナリのGZip断片を1チャンク取得する。Layout応答とは別の型が返るため送信口も分ける
        // Fetch one GZip slice of the terrain binary; it returns a different type than Layout, so it needs its own entry point
        public async UniTask<ResponseMapDataTerrainChunkMessagePack> GetTerrainChunk(int chunkIndex, CancellationToken ct)
        {
            var request = GetMapDataProtocol.RequestMapDataMessagePack.CreateTerrainChunkRequest(chunkIndex);
            return await _packetExchangeManager.GetPacketResponse<ResponseMapDataTerrainChunkMessagePack>(request, ct);
        }

        public async UniTask<WorldDataResponse> GetWorldData(CancellationToken ct)
        {
            var request = new RequestWorldDataProtocol.RequestWorldDataMessagePack();
            var (response, reason) = await _packetExchangeManager.GetPacketResponseWithReason<RequestWorldDataProtocol.ResponseWorldDataMessagePack>(request, ct);
            // 正常終了以外（タイムアウト等）はスキップし、呼び出し側 (WorldDataHandler) の null ガードに委ねる
            // Return null unless the exchange completed successfully so the caller (WorldDataHandler) can skip this update cycle
            if (reason != PacketWaitCompletionReason.Received) return null;

            return ParseWorldResponse(response);

            #region Internal

            WorldDataResponse ParseWorldResponse(RequestWorldDataProtocol.ResponseWorldDataMessagePack worldData)
            {
                var blocks = worldData.Blocks.Select(b => new BlockInfo(b));
                var entities = worldData.Entities.Select(e => new EntityResponse(e));

                return new WorldDataResponse(blocks.ToList(), entities.ToList());
            }

            #endregion
        }

        public async UniTask<BlockStateMessagePack> GetBlockState(Vector3Int blockPos, CancellationToken ct)
        {
            var request = new BlockStateProtocol.RequestBlockStateProtocolMessagePack(blockPos);
            var response = await _packetExchangeManager.GetPacketResponse<BlockStateProtocol.ResponseBlockStateProtocolMessagePack>(request, ct);

            return response.State;
        }

        // 指定ブロックが属するギアネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the gear network that the given block belongs to
        public async UniTask<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack> GetGearNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetGearNetworkInfoProtocol.RequestGetGearNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetGearNetworkInfoProtocol.ResponseGetGearNetworkInfoMessagePack>(request, ct);
        }

        // 指定ブロックが属する電力ネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the electric network that the given block belongs to
        public async UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> GetElectricNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack>(request, ct);
        }

        // 進行記録がセッション開始時に1回だけ読む。可変状態の同期ではないので初期データ取得のみ
        // The progress record reads this once at session start; it syncs no mutable state, so a fetch is enough
        public async UniTask<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack> GetWorldPlaySessionInfo(CancellationToken ct)
        {
            var request = new GetWorldPlaySessionInfoProtocol.RequestWorldPlaySessionInfoMessagePack();
            return await _packetExchangeManager.GetPacketResponse<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(request, ct);
        }
    }
}
