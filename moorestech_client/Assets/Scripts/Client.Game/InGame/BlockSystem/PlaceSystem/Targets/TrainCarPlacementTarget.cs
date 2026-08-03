using System;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class TrainCarPlacementTarget : IPlacementTarget
    {
        public readonly Guid TrainCarGuid;

        public Guid Id => TrainCarGuid;
        public PlacementTargetKind Kind => PlacementTargetKind.TrainCar;

        // 車両名の正はマスタ名。アイコン撮影時の表示名とは食い違い得るのでマスタ側に寄せる
        // The train car's canonical name is the master name; the icon-capture display name can drift from it
        public string DisplayName => MasterHolder.TrainUnitMaster.GetTrainCarMaster(TrainCarGuid).Name;

        public TrainCarPlacementTarget(Guid trainCarGuid)
        {
            TrainCarGuid = trainCarGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is TrainCarPlacementTarget target && TrainCarGuid == target.TrainCarGuid;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => TrainCarGuid.GetHashCode();
    }
}
