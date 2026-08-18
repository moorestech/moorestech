using System;
using System.Collections.Generic;
using System.Linq;
using Client.Localization;
using Core.Master;
using Game.PlacementTarget;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class TrainCarPlacementTarget : IPlacementTarget
    {
        public readonly Guid TrainCarGuid;

        public Guid Id => TrainCarGuid;
        public PlacementTargetKind Kind => PlacementTargetKind.TrainCar;

        public string DisplayName => Localize.GetContent(ContentLocalizationKeys.TrainCarName(TrainCarGuid));

        public IReadOnlyList<(Guid itemGuid, int count)> CreateRequiredItems()
        {
            var requiredItems = MasterHolder.TrainUnitMaster.GetTrainCarMaster(TrainCarGuid).RequiredItems;
            if (requiredItems == null) return Array.Empty<(Guid, int)>();
            return requiredItems.Select(item => (item.ItemGuid, item.Count)).ToList();
        }

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
