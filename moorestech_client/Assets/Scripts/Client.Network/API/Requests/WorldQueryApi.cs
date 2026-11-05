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

namespace Client.Network.API.Requests
{
    public sealed class WorldQueryApi
    {
        private const int MapDataRequestAttempts = 2;
        private const int MapDataTimeoutSeconds = 60;
        private readonly PacketExchangeManager _packetExchangeManager;

        public WorldQueryApi(PacketExchangeManager packetExchangeManager)
        {
            _packetExchangeManager = packetExchangeManager;
        }

        public async UniTask<SaveProtocol.SaveProtocolResponseMessagePack> Save(CancellationToken ct)
        {
            var request = new SaveProtocol.SaveProtocolMessagePack();
            return await _packetExchangeManager.GetPacketResponse<SaveProtocol.SaveProtocolResponseMessagePack>(request, ct);
        }

        public async UniTask<BugReportCaptureProtocol.BugReportCaptureResponse> RequestBugReportCapture(CancellationToken ct)
        {
            var request = BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest();
            return await _packetExchangeManager.GetPacketResponse<BugReportCaptureProtocol.BugReportCaptureResponse>(request, ct);
        }

        public async UniTask<List<GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new GetMapObjectInfoProtocol.RequestMapObjectInfosMessagePack();
            var response = await _packetExchangeManager.GetPacketResponse<GetMapObjectInfoProtocol.ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }

        // spawn/mapObjects/mapVeinsを取得
        // Fetch spawn/mapObjects/mapVeins
        public async UniTask<GetMapDataProtocol.ResponseMapDataMessagePack> GetMapData(CancellationToken ct)
        {
            // 地形ハッシュ計算で初回応答が遅れる場合は、読み取り専用のレイアウト要求を再送する
            // Retry the read-only layout request when the first response is delayed by terrain hashing
            for (var attempt = 1; attempt <= MapDataRequestAttempts; attempt++)
            {
                var request = GetMapDataProtocol.RequestMapDataMessagePack.CreateLayoutRequest();
                var (response, reason) = await _packetExchangeManager.GetPacketResponseWithReason<GetMapDataProtocol.ResponseMapDataMessagePack>(request, ct, MapDataTimeoutSeconds);
                if (reason == PacketWaitCompletionReason.Received) return response;

                Debug.LogWarning($"Map layout request attempt {attempt}/{MapDataRequestAttempts} ended with {reason}.");
                if (reason != PacketWaitCompletionReason.Timeout) break;
            }

            Debug.LogError("Map layout could not be fetched during initialization.");
            return null;
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
            // 正常終了以外（タイムアウト等）は null を返し、呼び出し側（初回ハンドシェイク）の判断に委ねる
            // Return null unless the exchange completed successfully and leave the decision to the caller (the initial handshake)
            if (reason != PacketWaitCompletionReason.Received) return null;

            return ParseWorldResponse(response);

            #region Internal

            WorldDataResponse ParseWorldResponse(RequestWorldDataProtocol.ResponseWorldDataMessagePack worldData)
            {
                var blocks = worldData.Blocks.Select(b => new BlockInfo(b));
                return new WorldDataResponse(blocks.ToList());
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

        // 機械の搬入が継承元のレベルで止まっているかを取得する
        // Fetch whether a machine's inflow is stalled on the inheritance source level
        public async UniTask<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack> GetMachineInputStall(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetMachineInputStallProtocol.RequestMachineInputStallMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack>(request, ct);
        }

        // 指定ブロックが属する電力ネットワークの現時点の集約値を取得する
        // Fetch current aggregate info of the electric network that the given block belongs to
        public async UniTask<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack> GetElectricNetworkInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetElectricNetworkInfoProtocol.RequestGetElectricNetworkInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetElectricNetworkInfoProtocol.ResponseGetElectricNetworkInfoMessagePack>(request, ct);
        }

        // 指定ブロックの部屋要約または漏れ箇所を返す
        // Returns room summaries or leak cells for the given block
        public async UniTask<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack> GetCleanRoomInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetCleanRoomInfoProtocol.RequestCleanRoomInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack>(request, ct);
        }

        // 選択工程の次回抽選の上限・確率を返す
        // Returns caps and chances for the selected process's next draw
        public async UniTask<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack> GetCleanRoomYieldInfo(BlockInstanceId blockInstanceId, CancellationToken ct)
        {
            var request = new GetCleanRoomYieldInfoProtocol.RequestCleanRoomYieldInfoMessagePack(blockInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack>(request, ct);
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
