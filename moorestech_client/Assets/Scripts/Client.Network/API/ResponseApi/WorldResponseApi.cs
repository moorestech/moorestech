using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.MapData;

namespace Client.Network.API
{
    public static class WorldResponseApi
    {
        // 保存を要求し、その要求番号を受け取る。書き出し完了は完了イベントと突き合わせる
        // Request a save and receive its generation; completion is matched against the completed event
        public static async UniTask<SaveProtocol.SaveProtocolResponseMessagePack> Save(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new SaveProtocol.SaveProtocolMessagePack();
            return await api.PacketExchange.GetPacketResponse<SaveProtocol.SaveProtocolResponseMessagePack>(request, ct);
        }

        // バグ報告用の即時スナップショットを要求する。完了は BugReportCaptureCompletedEventPacket.EventTag で届く
        // Requests an immediate snapshot for a bug report; completion arrives via BugReportCaptureCompletedEventPacket.EventTag
        public static async UniTask<BugReportCaptureProtocol.BugReportCaptureResponse> RequestBugReportCapture(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest();
            return await api.PacketExchange.GetPacketResponse<BugReportCaptureProtocol.BugReportCaptureResponse>(request, ct);
        }

        public static async UniTask<List<GetMapObjectInfoProtocol.MapObjectsInfoMessagePack>> GetMapObjectInfo(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new GetMapObjectInfoProtocol.RequestMapObjectInfosMessagePack();
            var response = await api.PacketExchange.GetPacketResponse<GetMapObjectInfoProtocol.ResponseMapObjectInfosMessagePack>(request, ct);
            return response?.MapObjects;
        }

        // 地形バイナリのGZip断片を1チャンク取得する。Layout応答とは別の型が返るため送信口も分ける
        // Fetch one GZip slice of the terrain binary; it returns a different type than Layout, so it needs its own entry point
        public static async UniTask<ResponseMapDataTerrainChunkMessagePack> GetTerrainChunk(this VanillaApiWithResponse api, int chunkIndex, CancellationToken ct)
        {
            var request = GetMapDataProtocol.RequestMapDataMessagePack.CreateTerrainChunkRequest(chunkIndex);
            return await api.PacketExchange.GetPacketResponse<ResponseMapDataTerrainChunkMessagePack>(request, ct);
        }

        public static async UniTask<WorldDataResponse> GetWorldData(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new RequestWorldDataProtocol.RequestWorldDataMessagePack(api.ConnectionSetting.PlayerId);
            var (response, reason) = await api.PacketExchange.GetPacketResponseWithReason<RequestWorldDataProtocol.ResponseWorldDataMessagePack>(request, ct);
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

        // 進行記録がセッション開始時に1回だけ読む。可変状態の同期ではないので初期データ取得のみ
        // The progress record reads this once at session start; it syncs no mutable state, so a fetch is enough
        public static async UniTask<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack> GetWorldPlaySessionInfo(this VanillaApiWithResponse api, CancellationToken ct)
        {
            var request = new GetWorldPlaySessionInfoProtocol.RequestWorldPlaySessionInfoMessagePack();
            return await api.PacketExchange.GetPacketResponse<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(request, ct);
        }
    }
}
