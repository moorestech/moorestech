using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Ui.State.PlacementTarget
{
    /// <summary>
    /// 配置対象 → 絵の出どころの ID（Web の橋 ResolveIconUrl が指す絵と同じもの）
    /// Placement target to the picture-source ID (the same picture the Web bridge ResolveIconUrl points at)
    /// </summary>
    public static class PlacementTargetIconResolver
    {
        public static PlacementTargetIcon Resolve(IPlacementTarget target, IBlueprintThumbnailLookup thumbnails)
        {
            return target switch
            {
                BlockPlacementTarget block => new PlacementTargetBlockIcon(block.BlockId),
                TrainCarPlacementTarget trainCar => new PlacementTargetTrainCarIcon(trainCar.TrainCarGuid),
                ConnectToolPlacementTarget connectTool => new PlacementTargetConnectToolIcon(connectTool.ConnectToolGuid),
                // 撮影済みかの判定だけに thumbnails を使う。未撮影は名前で描く（Web の iconUrl = null）
                // thumbnails only answers whether it is captured; an uncaptured blueprint is drawn by name (Web iconUrl = null)
                BlueprintPlacementTarget blueprint => thumbnails.Contains(blueprint.BlueprintGuid) ? new PlacementTargetBlueprintIcon(blueprint.BlueprintGuid) : PlacementTargetNameOnlyIcon.Instance,
                BlueprintCopyPlacementTarget => PlacementTargetNameOnlyIcon.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null),
            };
        }
    }
}
