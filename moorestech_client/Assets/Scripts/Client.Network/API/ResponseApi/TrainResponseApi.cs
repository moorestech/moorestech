using System;
using System.Threading;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Block.Blocks.TrainRail;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;

namespace Client.Network.API
{
    public class TrainResponseApi
    {
        private readonly PacketExchangeManager _packetExchange;
        private readonly PlayerConnectionSetting _connectionSetting;

        public TrainResponseApi(PacketExchangeManager packetExchangeManager, PlayerConnectionSetting playerConnectionSetting)
        {
            _packetExchange = packetExchangeManager;
            _connectionSetting = playerConnectionSetting;
        }

        // train/rail再同期の引き金を送る。snapshot本体はイベント経路で届く
        // Send the resync trigger; snapshots arrive over the event stream
        public async UniTask<TrainResyncProtocol.ResponseMessagePack> SendTrainResync(bool includeRailGraph, CancellationToken ct)
        {
            var request = new TrainResyncProtocol.RequestMessagePack(includeRailGraph);
            return await _packetExchange.GetPacketResponse<TrainResyncProtocol.ResponseMessagePack>(request, ct);
        }

        // 新規編成としてレールへ列車車両を設置する
        // Place a train car on a rail as a new train unit
        public async UniTask<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack> PlaceTrainOnRail(RailPosition railPosition, Guid trainCarGuid, CancellationToken ct)
        {
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition?.CreateSaveSnapshot());
            var request = new PlaceTrainCarOnRailProtocol.PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, trainCarGuid, _connectionSetting.PlayerId);
            return await _packetExchange.GetPacketResponse<PlaceTrainCarOnRailProtocol.PlaceTrainOnRailResponseMessagePack>(request, ct);
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
                _connectionSetting.PlayerId,
                attachCarFacingForward,
                attachToTargetTrainHead);
            return await _packetExchange.GetPacketResponse<AttachTrainCarToUnitProtocol.AttachTrainCarToUnitResponseMessagePack>(request, ct);
        }

        // 貨物プラットフォームのロード/アンロードモードを切り替える
        // Switch the load/unload transfer mode of a train platform block
        public async UniTask<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse> SetTrainPlatformTransferMode(
            Vector3Int position, TrainPlatformTransferComponent.TransferMode mode, CancellationToken ct)
        {
            var request = new SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeRequest(position, mode);
            return await _packetExchange.GetPacketResponse<SetTrainPlatformTransferModeProtocol.SetTrainPlatformTransferModeResponse>(request, ct);
        }

        // 時刻表置換と自動運転切替を単一の送信口で扱う
        // Send timetable replacement and auto-run changes through one entry point
        public async UniTask<TrainScheduleEditProtocol.TrainScheduleEditResponse> SendTrainScheduleEdit(
            TrainScheduleEditProtocol.TrainScheduleEditRequest request, CancellationToken ct)
        {
            return await _packetExchange.GetPacketResponse<TrainScheduleEditProtocol.TrainScheduleEditResponse>(request, ct);
        }

        // 時刻表タブを開いたときに現在の時刻表を取り寄せる
        // Fetch the current timetable when the timetable tab opens
        public async UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(
            TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
        {
            var request = new GetTrainTimetableProtocol.GetTrainTimetableRequest(trainUnitInstanceId);
            return await _packetExchange.GetPacketResponse<GetTrainTimetableProtocol.GetTrainTimetableResponse>(request, ct);
        }

        // 改名結果を待ち、表示更新はブロック状態の通知に委ねる
        // Await the rename result; block state notifications update the displayed name
        public async UniTask<SetTrainStationNameProtocol.SetTrainStationNameResponse> SetTrainStationName(
            Vector3Int position, string stationName, CancellationToken ct)
        {
            var request = new SetTrainStationNameProtocol.SetTrainStationNameRequest(position, stationName);
            return await _packetExchange.GetPacketResponse<SetTrainStationNameProtocol.SetTrainStationNameResponse>(request, ct);
        }
    }
}
