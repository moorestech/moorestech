using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Game.Train.Unit;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    // 開いている車両インベントリから所属列車IDを引く
    // Resolve the owning train id of the open car inventory from received snapshots
    public static class OpenTrainUnitResolver
    {
        public static bool TryResolveOpenTrain(SubInventoryState state, TrainUnitClientCache cache, out TrainUnitInstanceId trainUnitInstanceId)
        {
            trainUnitInstanceId = default;
            if (state.CurrentSubInventorySource is not TrainSubInventorySource source) return false;
            return TryResolveOwningTrain(source.TrainCarInstanceId, cache, out trainUnitInstanceId);
        }

        public static bool TryResolveOwningTrain(long trainCarInstanceId, TrainUnitClientCache cache, out TrainUnitInstanceId trainUnitInstanceId)
        {
            trainUnitInstanceId = default;
            if (!cache.TryGetCarSnapshot(new TrainCarInstanceId(trainCarInstanceId), out var unit, out _, out _, out _)) return false;
            trainUnitInstanceId = unit.TrainUnitInstanceId;
            return true;
        }
    }
}
