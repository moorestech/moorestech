using System;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Util;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;

namespace Game.Block.Blocks.CleanRoom
{
    // プロセッサの稼働状態をクライアント向け BlockStateDetail 群へ写像する presenter。
    // Presenter that maps the processor's runtime state to the client-facing BlockStateDetail array.
    public static class CleanRoomMachineProcessorStateDetail
    {
        public static BlockStateDetail[] Create(float currentPower, float requestPower, float processingRate,
            ProcessState currentState, ProcessState lastState, Guid recipeGuid)
        {
            var commonMachineBlock = CommonMachineBlockStateDetail.CreateState(currentPower, requestPower, processingRate, currentState.ToStr(), lastState.ToStr());
            var machineBlock = MachineBlockStateDetail.CreateState(processingRate, recipeGuid);
            return new[] { commonMachineBlock, machineBlock };
        }
    }
}
