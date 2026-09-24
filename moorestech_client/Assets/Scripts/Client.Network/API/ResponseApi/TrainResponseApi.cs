using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Block.Blocks.TrainRail;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public static class TrainResponseApi
    {
        // train/rail再同期の引き金を送る。snapshot本体はイベント経路で届く
        // Send the resync trigger; snapshots arrive over the event stream
        public static async UniTask<TrainResyncProtocol.ResponseMessagePack> SendTrainResync(this VanillaApiWithResponse api, bool includeRailGraph, CancellationToken ct)
        {
            var request = new TrainResyncProtocol.RequestMessagePack(includeRailGraph);
            return await api.PacketExchange.GetPacketResponse<TrainResyncProtocol.ResponseMessagePack>(request, ct);
        }

        public static async UniTask<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack> AttachTrainCarToUnit(
            this VanillaApiWithResponse api,
            TrainUnitInstanceId targetTrainUnitInstanceId,
            RailPosition railPosition,
            Guid trainCarGuid,
            bool attachCarFacingForward,
            bool attachToTargetTrainHead,
            CancellationToken ct)
        {
            // 既存編成連結のレスポンスを取得する
            // Get response for attaching a car to an existing train unit
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition?.CreateSaveSnapshot());
            var request = new AttachTrainCarToUnitProtocol.AttachTrainCarToUnitRequestMessagePack(
                targetTrainUnitInstanceId,
                railPositionSnapshot,
                trainCarGuid,
                api.ConnectionSetting.PlayerId,
                attachCarFacingForward,
                attachToTargetTrainHead);
            return await api.PacketExchange.GetPacketResponse<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack>(request, ct);
        }

        // 貨物プラットフォームのロード/アンロードモードを切り替える
        // Switch the load/unload transfer mode of a train platform block
        public static async UniTask<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse> SetTrainPlatformTransferMode(
            this VanillaApiWithResponse api,
            Vector3Int position, TrainPlatformTransferComponent.TransferMode mode, CancellationToken ct)
        {
            var request = new SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeRequest(position, mode);
            return await api.PacketExchange.GetPacketResponse<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse>(request, ct);
        }
    }
}
