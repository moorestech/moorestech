using Game.CleanRoom;
using Game.Gear.Common;
using Game.PlayerRiding;
using Game.Train.Diagram;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Event.EventReceive.UnifiedInventoryEvent;

namespace Server.Boot.DependencyInjection
{
    internal static class ServerEntryPointMaterializer
    {
        public static void Materialize(ServiceProvider provider)
        {
            // インベントリ・配置・チャレンジの受信口を既存順で生成する
            // Materialize inventory, placement, and challenge receivers in the existing order.
            provider.GetService<MainInventoryUpdateEventPacket>();
            provider.GetService<UnifiedInventoryEventPacket>();
            provider.GetService<GrabInventoryUpdateEventPacket>();
            provider.GetService<PlaceBlockEventPacket>();
            provider.GetService<RemoveBlockToSetEventPacket>();
            provider.GetService<CompletedChallengeEventPacket>();

            // 共有ネットワークと鉄道管理サービスを既存順で生成する
            // Materialize shared network and rail managers in the existing order.
            provider.GetService<GearNetworkDatastore>();
            provider.GetService<CleanRoomDatastore>();
            provider.GetService<RailGraphDatastore>();
            provider.GetService<TrainDiagramManager>();
            provider.GetService<TrainRailPositionManager>();

            // 状態変更・研究・鉄道イベントの受信口を既存順で生成する
            // Materialize state, research, and rail event receivers in the existing order.
            provider.GetService<ChangeBlockStateEventPacket>();
            provider.GetService<MapObjectUpdateEventPacket>();
            provider.GetService<UnlockedEventPacket>();
            provider.GetService<ResearchCompleteEventPacket>();
            provider.GetService<ItemStackLevelUnlockEventPacket>();
            provider.GetService<RailNodeCreatedEventPacket>();
            provider.GetService<RailConnectionCreatedEventPacket>();
            provider.GetService<TrainUnitTickDiffBundleEventPacket>();
            provider.GetService<TrainUnitSnapshotEventPacket>();
            provider.GetService<RailNodeRemovedEventPacket>();
            provider.GetService<RailConnectionRemovedEventPacket>();
            provider.GetService<RidingStateEventPacket>();
            provider.GetService<RemovedRidableRidingHandler>();
        }
    }
}
