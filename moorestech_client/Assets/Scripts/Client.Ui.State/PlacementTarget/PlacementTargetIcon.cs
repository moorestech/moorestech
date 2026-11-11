using System;
using Core.Master;

namespace Client.Ui.State.PlacementTarget
{
    /// <summary>
    /// 配置対象の絵の出どころ（ID だけ）。絵は Client.Ui の PlacementTargetIcons が引く
    /// Where a placement target's picture comes from (IDs only); Client.Ui PlacementTargetIcons looks the picture up
    /// </summary>
    public abstract class PlacementTargetIcon
    {
        private protected PlacementTargetIcon()
        {
        }
    }

    public sealed class PlacementTargetBlockIcon : PlacementTargetIcon
    {
        public BlockId BlockId { get; }

        public PlacementTargetBlockIcon(BlockId blockId)
        {
            BlockId = blockId;
        }

        public override bool Equals(object obj) => obj is PlacementTargetBlockIcon other && other.BlockId == BlockId;
        public override int GetHashCode() => BlockId.GetHashCode();
    }

    public sealed class PlacementTargetTrainCarIcon : PlacementTargetIcon
    {
        public Guid TrainCarGuid { get; }

        public PlacementTargetTrainCarIcon(Guid trainCarGuid)
        {
            TrainCarGuid = trainCarGuid;
        }

        public override bool Equals(object obj) => obj is PlacementTargetTrainCarIcon other && other.TrainCarGuid == TrainCarGuid;
        public override int GetHashCode() => TrainCarGuid.GetHashCode();
    }

    public sealed class PlacementTargetConnectToolIcon : PlacementTargetIcon
    {
        public Guid ConnectToolGuid { get; }

        public PlacementTargetConnectToolIcon(Guid connectToolGuid)
        {
            ConnectToolGuid = connectToolGuid;
        }

        public override bool Equals(object obj) => obj is PlacementTargetConnectToolIcon other && other.ConnectToolGuid == ConnectToolGuid;
        public override int GetHashCode() => ConnectToolGuid.GetHashCode();
    }

    // 撮影済みの BP だけ（未撮影は NameOnly）。値の等価を持たない: Guid が同じでも撮り直しで絵が変わるので、組み直した VM を別物として配る
    // Captured blueprints only (uncaptured ones are NameOnly); no value equality: a retake changes the picture under the same Guid, so a rebuilt VM is delivered as new
    public sealed class PlacementTargetBlueprintIcon : PlacementTargetIcon
    {
        public Guid BlueprintGuid { get; }

        public PlacementTargetBlueprintIcon(Guid blueprintGuid)
        {
            BlueprintGuid = blueprintGuid;
        }
    }

    // 絵が無く名前で描く（Web の iconUrl = null。未撮影の BP・BP コピー）。1 つしか無いので参照の等価が値の等価
    // No picture, drawn by name (Web iconUrl = null); a singleton, so reference equality is value equality
    public sealed class PlacementTargetNameOnlyIcon : PlacementTargetIcon
    {
        public static readonly PlacementTargetNameOnlyIcon Instance = new();

        private PlacementTargetNameOnlyIcon()
        {
        }
    }
}
