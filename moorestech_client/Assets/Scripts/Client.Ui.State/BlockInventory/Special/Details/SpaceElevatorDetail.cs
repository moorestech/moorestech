using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Logic.Special;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 宇宙エレベーターの詳細。入口・出口は配られた順のままサーバーの絶対添字（Web SpaceElevatorDetailDto と同じ）
    /// Space elevator detail; inlet and outlets keep the published order as absolute server indices (same as Web SpaceElevatorDetailDto)
    /// </summary>
    public sealed class SpaceElevatorDetail
    {
        public IReadOnlyList<int> SatelliteInputSlots { get; }
        public IReadOnlyList<int> OutputSlots { get; }
        // 入口の枠に運べる積荷だけを受ける Shift の束縛
        // Shift bindings accepting only the carried payloads on the inlet slot
        public IReadOnlyList<InletSlotBinding> InletBindings { get; }
        public bool Connected { get; }
        public int CablePoolAmount { get; }
        public int CablesPerElevator { get; }
        public float ClimbProgress { get; }
        internal SpaceElevatorBaseStatus Status { get; }

        // 状態の文言と不足色・接続できるかは State で決める（Screens へサーバーの列挙を出さない。D-27）
        // Status text, shortage tone and connectability are decided in State (no server enums reach Screens. D-27)
        public SpaceElevatorStatusDisplay StatusDisplay => SpaceElevatorLogic.StatusDisplay(Status);
        public bool CanConnect => Status == SpaceElevatorBaseStatus.ReadyToConnect;

        public SpaceElevatorDetail(IReadOnlyList<int> satelliteInputSlots, IReadOnlyList<int> outputSlots, IReadOnlyList<InletSlotBinding> inletBindings, bool connected, int cablePoolAmount, int cablesPerElevator, float climbProgress, SpaceElevatorBaseStatus status)
        {
            SatelliteInputSlots = satelliteInputSlots;
            OutputSlots = outputSlots;
            InletBindings = inletBindings;
            Connected = connected;
            CablePoolAmount = cablePoolAmount;
            CablesPerElevator = cablesPerElevator;
            ClimbProgress = climbProgress;
            Status = status;
        }
    }
}
