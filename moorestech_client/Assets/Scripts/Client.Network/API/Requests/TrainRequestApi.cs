using System;
using System.Threading;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Blocks.TrainRail;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.PlayerRiding.Interface;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API.Requests
{
    public sealed class TrainRequestApi
    {
        private readonly PacketExchangeManager _packetExchangeManager;

        public TrainRequestApi(PacketExchangeManager packetExchangeManager)
        {
            _packetExchangeManager = packetExchangeManager;
        }

        public async UniTask<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack> PlaceTrainOnRail(RailPosition railPosition, Guid trainCarGuid, CancellationToken ct)
        {
            // 列車設置のレスポンスを取得する
            // Get response for train placement
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition?.CreateSaveSnapshot());
            var request = new PlaceTrainCarOnRailProtocol.PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, trainCarGuid);
            return await _packetExchangeManager.GetPacketResponse<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack>(request, ct);
        }

        public async UniTask<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack> AttachTrainCarToUnit(
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
                attachCarFacingForward,
                attachToTargetTrainHead);
            return await _packetExchangeManager.GetPacketResponse<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack>(request, ct);
        }

        // 乗車/降車をサーバーに要求し、結果を受け取る（仕様書セクション5.1）。
        // Requests ride/dismount from the server and returns the result.
        public async UniTask<RideActionProtocol.ResponseRideActionMessagePack> RideAction(RideActionType action, RidableIdentifierMessagePack target, CancellationToken ct)
        {
            var request = new RideActionProtocol.RequestRideActionMessagePack(action, target);
            return await _packetExchangeManager.GetPacketResponse<RideActionProtocol.ResponseRideActionMessagePack>(request, ct);
        }

        // 貨物プラットフォームのロード/アンロードモードを切り替える
        // Switch the load/unload transfer mode of a train platform block
        public async UniTask<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse> SetTrainPlatformTransferMode(
            Vector3Int position, TrainPlatformTransferComponent.TransferMode mode, CancellationToken ct)
        {
            var request = new SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeRequest(position, mode);
            return await _packetExchangeManager.GetPacketResponse<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse>(request, ct);
        }

        public async UniTask<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse> PlaceRailWithPier(
            int fromNodeId,
            Guid fromGuid,
            BlockId pierBlockId,
            PlaceInfo pierPlaceInfo,
            Guid railTypeGuid,
            CancellationToken ct)
        {
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(fromNodeId, fromGuid, pierBlockId, pierPlaceInfo, railTypeGuid);
            return await _packetExchangeManager.GetPacketResponse<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(request, ct);
        }

        // 時刻表置換と自動運転切替を単一の送信口で扱う
        // Send timetable replacement and auto-run changes through one entry point
        public async UniTask<TrainScheduleEditProtocol.TrainScheduleEditResponse> SendTrainScheduleEdit(
            TrainScheduleEditProtocol.TrainScheduleEditRequest request, CancellationToken ct)
        {
            return await _packetExchangeManager.GetPacketResponse<TrainScheduleEditProtocol.TrainScheduleEditResponse>(request, ct);
        }

        // 時刻表タブを開いたときに現在の時刻表を取り寄せる
        // Fetch the current timetable when the timetable tab opens
        public async UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(
            TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
        {
            var request = new GetTrainTimetableProtocol.GetTrainTimetableRequest(trainUnitInstanceId);
            return await _packetExchangeManager.GetPacketResponse<GetTrainTimetableProtocol.GetTrainTimetableResponse>(request, ct);
        }

        // 改名結果を待ち、表示更新はブロック状態の通知に委ねる
        // Await the rename result; block state notifications update the displayed name
        public async UniTask<SetTrainStationNameProtocol.SetTrainStationNameResponse> SetTrainStationName(
            Vector3Int position, string stationName, CancellationToken ct)
        {
            var request = new SetTrainStationNameProtocol.SetTrainStationNameRequest(position, stationName);
            return await _packetExchangeManager.GetPacketResponse<SetTrainStationNameProtocol.SetTrainStationNameResponse>(request, ct);
        }
    }
}
