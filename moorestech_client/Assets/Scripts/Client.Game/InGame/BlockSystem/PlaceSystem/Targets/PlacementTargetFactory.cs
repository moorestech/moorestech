using System;
using Core.Master;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public static class PlacementTargetFactory
    {
        // カタログエントリからIPlacementTargetを生成する唯一の解決点
        // The single resolution point from catalog entry to IPlacementTarget
        public static IPlacementTarget Create(PlacementTargetEntry entry)
        {
            switch (entry.Kind)
            {
                case PlacementTargetKind.Block:
                    return new BlockPlacementTarget(entry.Id, null);
                case PlacementTargetKind.TrainCar:
                    return new TrainCarPlacementTarget(entry.Id);
                case PlacementTargetKind.ConnectTool:
                    return new ConnectToolPlacementTarget(entry.Id);
                case PlacementTargetKind.BlueprintCopy:
                    return new BlueprintCopyPlacementTarget(entry.Id);
                case PlacementTargetKind.Blueprint:
                    return new BlueprintPlacementTarget(entry.Id, entry.MasterDisplayName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Kind), entry.Kind, null);
            }
        }
    }
}
