using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using Client.Ui.State.BlockInventory.Logic.Special;
using Game.Block.Interface.State;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// 発射台の詳細。資材・積荷の枠はサーバーの絶対添字、出力は先頭から在庫の末尾まで（Web RocketLaunchPadDetailDtoBuilder と同じ）
    /// Launch pad detail; material and payload slots are absolute server indices, outputs run from the start to the inventory end (same as the Web builder)
    /// </summary>
    public sealed class RocketLaunchPadDetail
    {
        internal RocketLaunchPadPhase Phase { get; }
        internal RocketLaunchPadWaitReason WaitReason { get; }
        public float Progress { get; }
        public IReadOnlyList<int> MaterialSlots { get; }
        public IReadOnlyList<int> PayloadSlots { get; }
        private int OutputSlotStart { get; }

        public RocketLaunchPadDetail(RocketLaunchPadPhase phase, RocketLaunchPadWaitReason waitReason, float progress, IReadOnlyList<int> materialSlots, IReadOnlyList<int> payloadSlots, int outputSlotStart)
        {
            Phase = phase;
            WaitReason = waitReason;
            Progress = progress;
            MaterialSlots = materialSlots;
            PayloadSlots = payloadSlots;
            OutputSlotStart = outputSlotStart;
        }

        // 状態文と題の操作は State で引き、Screens へサーバーの列挙を出さない（D-27）
        // Resolve the status text and title action in State so Screens never sees the server enums (D-27)
        public LocalizationKey StatusKey => RocketLaunchPadLogic.StatusKey(Phase, WaitReason);

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
