using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Items;
using Mooresmaster.Localization.Generated;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Logic.Special;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 発射台の詳細。資材・積荷の枠はサーバーの絶対添字と必要な品・数、出力は先頭から在庫の末尾まで、積荷の選択（ADR 0106）
    /// Launch pad detail: material and payload slots as absolute indices with required items and counts, outputs from the start to the inventory end, and the cargo selection (ADR 0106)
    /// </summary>
    public sealed class RocketLaunchPadDetail
    {
        internal RocketLaunchPadPhase Phase { get; }
        internal RocketLaunchPadWaitReason WaitReason { get; }
        public float Progress { get; }
        public IReadOnlyList<RequiredItemSlot> MaterialSlots { get; }
        public IReadOnlyList<RequiredItemSlot> PayloadSlots { get; }
        // 未選択は空、選べば資材と選んだ積荷の枠だけを受ける Shift の束縛
        // Shift bindings: empty when unselected, otherwise only material slots and the selected payload slot
        public IReadOnlyList<InletSlotBinding> InletBindings { get; }
        public OrbitalCargoDetail Cargo { get; }
        private int OutputSlotStart { get; }

        public RocketLaunchPadDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, IReadOnlyList<RequiredItemSlot> materialSlots, IReadOnlyList<RequiredItemSlot> payloadSlots, IReadOnlyList<InletSlotBinding> inletBindings, int outputSlotStart, OrbitalCargoDetail cargo)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
            MaterialSlots = materialSlots;
            PayloadSlots = payloadSlots;
            InletBindings = inletBindings;
            OutputSlotStart = outputSlotStart;
            Cargo = cargo;
        }

        // 状態文と題の操作は State で引き、Screens へサーバーの列挙を出さない（D-27）
        // Resolve the status text and title action in State so Screens never sees the server enums (D-27)
        public LocalizationKey StatusKey => RocketLaunchPadLogic.StatusKey(Phase, WaitReason);

        // 資材の枠（透かしつき）と、選んだ積荷の枠＋前の種類の残り
        // Material slots (ghosted) and the selected payload slot plus leftovers of previous kinds
        public IReadOnlyList<BoundItemSlot> MaterialBoundSlots => OrbitalCargoSlotBinding.Materials(MaterialSlots);

        public IReadOnlyList<BoundItemSlot> PayloadBoundSlots(IReadOnlyList<SlotItem> itemSlots)
        {
            return OrbitalCargoSlotBinding.Payloads(PayloadSlots, Cargo, itemSlots);
        }

        public bool TryGetCommandButton(out RocketCommandButton button)
        {
            return RocketLaunchPadLogic.TryGetCommandButton(Phase, out button);
        }

        // 出力の枠（在庫の数が先頭に届かなければ空）
        // Output slots; empty when the inventory does not reach the start
        public IReadOnlyList<int> OutputSlots(int itemSlotCount)
        {
            var slots = new List<int>();
            for (var slot = OutputSlotStart; slot < itemSlotCount; slot++) slots.Add(slot);
            return slots;
        }
    }
}
