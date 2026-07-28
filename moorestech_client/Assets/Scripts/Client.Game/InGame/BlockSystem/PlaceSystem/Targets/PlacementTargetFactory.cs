using Core.Master;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public static class PlacementTargetFactory
    {
        // カタログエントリからIPlacementTargetを生成する唯一の解決点
        // The single resolution point from catalog entry to IPlacementTarget
        public static bool TryCreate(PlacementTargetEntry entry, out IPlacementTarget target)
        {
            switch (entry.Kind)
            {
                case PlacementTargetKind.Block:
                    // ブロックだけは設置処理が揮発BlockIdを要求するため、ここでGuidから解決する
                    // Only blocks need the volatile BlockId for placement, so resolve it from the Guid here
                    target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockId(entry.Id), null);
                    return true;
                case PlacementTargetKind.TrainCar:
                    target = new TrainCarPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.ConnectTool:
                    target = new ConnectToolPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.BuildTool:
                    target = new BuildToolPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.Blueprint:
                    target = new BlueprintPlacementTarget(entry.Id, entry.DisplayName);
                    return true;
                default:
                    target = null;
                    return false;
            }
        }
    }
}
