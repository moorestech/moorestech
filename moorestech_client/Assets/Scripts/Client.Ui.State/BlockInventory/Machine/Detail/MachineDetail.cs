using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;

namespace Client.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の詳細の写し
    /// Snapshot of a machine's detail: selection, state, power, progress, running recipe, slot binding
    /// </summary>
    public sealed class MachineDetail
    {
        public MachineRecipeSelection Selection { get; }
        public MachineProcessDisplayState State { get; }
        public double CurrentPower { get; }
        public double RequestPower { get; }
        public float Progress { get; }
        public bool OutputSpaceShort { get; }
        public bool GearDriven { get; }
        public double RunningRecipeTimeSeconds { get; }
        public IReadOnlyList<RecipeOutputItem> RunningRecipeOutputs { get; }
        public MachineBindingDetail Binding { get; }

        public MachineDetail(MachineRecipeSelection selection, MachineProcessDisplayState state, double currentPower, double requestPower, float progress, bool outputSpaceShort, bool gearDriven, double runningRecipeTimeSeconds, IReadOnlyList<RecipeOutputItem> runningRecipeOutputs, MachineBindingDetail binding)
        {
            Selection = selection;
            State = state;
            CurrentPower = currentPower;
            RequestPower = requestPower;
            Progress = progress;
            OutputSpaceShort = outputSpaceShort;
            GearDriven = gearDriven;
            RunningRecipeTimeSeconds = runningRecipeTimeSeconds;
            RunningRecipeOutputs = runningRecipeOutputs;
            Binding = binding;
        }
    }
}
