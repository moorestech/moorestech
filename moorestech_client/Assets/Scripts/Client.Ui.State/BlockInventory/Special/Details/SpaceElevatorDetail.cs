using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 宇宙エレベーターの詳細。入口・出口は配られた順のままサーバーの絶対添字、積荷の選択（ADR 0106 の 7）
    /// Space elevator detail; inlet and outlets keep the published order as absolute server indices, plus the cargo selection (ADR 0106 §7)
    /// </summary>
    public sealed class SpaceElevatorDetail
    {
        public IReadOnlyList<int> SatelliteInputSlots { get; }
        public IReadOnlyList<int> OutputSlots { get; }
        // 未選択は空、選べば入口に選んだ種類だけを受ける Shift の束縛
        // Shift bindings: empty when unselected, otherwise only the selected kind on the inlet
        public IReadOnlyList<InletSlotBinding> InletBindings { get; }
        public bool Connected { get; }
        public int CablePoolAmount { get; }
        public int CablesPerElevator { get; }
        public float ClimbProgress { get; }
        public OrbitalCargoDetail Cargo { get; }
        internal SpaceElevatorBaseStatus Status { get; }

        // 状態の文言と不足色・接続できるかは State で決める（Screens へサーバーの列挙を出さない。D-27）
        // Status text, shortage tone and connectability are decided in State (no server enums reach Screens. D-27)
        public SpaceElevatorStatusDisplay StatusDisplay => SpaceElevatorLogic.StatusDisplay(Status);
        public bool CanConnect => Status == SpaceElevatorBaseStatus.ReadyToConnect;
        // 入口の枠（選んでいれば 1 便ぶんの透かし）
        // The inlet slot, ghosted with one flight when selected
        public IReadOnlyList<BoundItemSlot> InletBoundSlots => OrbitalCargoSlotBinding.Inlet(SatelliteInputSlots[0], Cargo);

        public SpaceElevatorDetail(IReadOnlyList<int> satelliteInputSlots, IReadOnlyList<int> outputSlots, IReadOnlyList<InletSlotBinding> inletBindings, bool connected, int cablePoolAmount, int cablesPerElevator, float climbProgress, SpaceElevatorBaseStatus status, OrbitalCargoDetail cargo)
        {
            SatelliteInputSlots = satelliteInputSlots;
            OutputSlots = outputSlots;
            InletBindings = inletBindings;
            Connected = connected;
            CablePoolAmount = cablePoolAmount;
            CablesPerElevator = cablesPerElevator;
            ClimbProgress = climbProgress;
            Status = status;
            Cargo = cargo;
        }
    }
}
