using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.UnlockState;
using Game.UnlockState.States;

namespace Client.Tests.Ui.State.BuildMenu
{
    /// <summary>
    /// ブロック・車両・接続ツール・BP を全解放するスタブ（前例 BuildMenuEntryDtoFactoryTest の写し）
    /// Stub that unlocks every block, train car, connect tool, and blueprint (copy of the BuildMenuEntryDtoFactoryTest precedent)
    /// </summary>
    internal sealed class AllPlacementTargetsUnlocked : IGameUnlockStateData
    {
        public IReadOnlyDictionary<Guid, BlockUnlockStateInfo> BlockUnlockStateInfos { get; } =
            MasterHolder.BlockMaster.Blocks.Data.ToDictionary(b => b.BlockGuid, b => new BlockUnlockStateInfo(b.BlockGuid, true));

        public IReadOnlyDictionary<Guid, ConnectToolUnlockStateInfo> ConnectToolUnlockStateInfos { get; } =
            MasterHolder.ConnectToolMaster.All.ToDictionary(c => c.ConnectToolGuid, c => new ConnectToolUnlockStateInfo(c.ConnectToolGuid, true));

        public IReadOnlyDictionary<Guid, CraftRecipeUnlockStateInfo> CraftRecipeUnlockStateInfos { get; } = new Dictionary<Guid, CraftRecipeUnlockStateInfo>();
        public IReadOnlyDictionary<ItemId, ItemUnlockStateInfo> ItemUnlockStateInfos { get; } = new Dictionary<ItemId, ItemUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, ChallengeCategoryUnlockStateInfo> ChallengeCategoryUnlockStateInfos { get; } = new Dictionary<Guid, ChallengeCategoryUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, MachineRecipeUnlockStateInfo> MachineRecipeUnlockStateInfos { get; } = new Dictionary<Guid, MachineRecipeUnlockStateInfo>();
        public IReadOnlyDictionary<Guid, TrainCarUnlockStateInfo> TrainCarUnlockStateInfos { get; } =
            MasterHolder.TrainUnitMaster.Train.TrainCars.ToDictionary(
                trainCar => trainCar.TrainCarGuid,
                trainCar => new TrainCarUnlockStateInfo(trainCar.TrainCarGuid, true));

        public bool IsBlueprintUnlocked => true;
    }
}
