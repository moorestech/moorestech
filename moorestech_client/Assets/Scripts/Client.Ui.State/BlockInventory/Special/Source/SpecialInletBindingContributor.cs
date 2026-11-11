using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Logic.SlotRestriction;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SlotRestriction;
using Core.Master;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// 発射台・宇宙エレベーターの入れ口が受け付ける物を S4-alpha の Shift の束縛へ寄与する（Web の slotBindings と同じ。状態の未着は寄与せず取得待ち）
    /// Contributes what the launch pad and space elevator inlets accept to S4-alpha's Shift bindings (same as the Web slotBindings; no contribution while the state is missing, so it stays pending)
    /// </summary>
    public sealed class SpecialInletBindingContributor : IBlockSlotRestrictionContributor
    {
        private readonly OpenBlockDetailTracker _tracker;

        public SpecialInletBindingContributor(OpenBlockDetailTracker tracker)
        {
            _tracker = tracker;
        }

        public bool AppliesTo(OpenBlockInventoryPanel panel)
        {
            return panel.BlockType == BlockMasterElement.BlockTypeConst.RocketLaunchPad || panel.BlockType == BlockMasterElement.BlockTypeConst.SpaceElevatorBase;
        }

        public OpenBlockSlotInput Contribute(OpenBlockInventoryPanel panel, OpenBlockSlotInput input)
        {
            // パネルは識別子だけなので、追跡ブロックを BlockInstanceId で照らして中身を読む（D-26）
            // The panel only carries identifiers, so read contents from the tracked block matched by BlockInstanceId (D-26)
            var block = _tracker.TrackedBlock;
            // silent-ok: 追跡の差し替え途中・状態の未着は寄与しない（L0-s4b が入れ口固定のブロックを取得待ちへ倒す） / alpha の Shift が取得待ちの通知（状態の取得を待っています）を出して気づく
            if (block == null || !block.BlockInstanceId.Equals(panel.BlockInstanceId)) return input;
            return TryGetInletBindings(block, out var bindings) ? input.WithInletBindings(bindings) : input;
        }

        private static bool TryGetInletBindings(BlockGameObject block, out IReadOnlyList<InletSlotBinding> bindings)
        {
            bindings = null;
            var param = block.BlockMasterElement.BlockParam;

            // 発射台: 資材と積荷の枠ごとに要求の 1 品
            // Launch pad: one required item per material and payload slot
            if (param is RocketLaunchPadBlockParam rocketParam)
            {
                if (block.GetStateDetail<RocketLaunchPadStateDetail>(RocketLaunchPadStateDetail.BlockStateDetailKey) == null) return false;
                var layout = new RocketLaunchPadSlotLayout(rocketParam);
                var list = new List<InletSlotBinding>();
                foreach (var slot in layout.MaterialSlots) list.Add(new InletSlotBinding(slot, new[] { layout.RequirementAt(slot).ItemId }));
                foreach (var slot in layout.PayloadSlots) list.Add(new InletSlotBinding(slot, new[] { layout.RequirementAt(slot).ItemId }));
                bindings = list;
                return true;
            }

            // 宇宙エレベーター: 入口 1 枠に運べる積荷
            // Space elevator: the carried payloads on the single inlet slot
            if (param is SpaceElevatorBaseBlockParam elevatorParam)
            {
                if (block.GetStateDetail<SpaceElevatorBaseStateDetail>(SpaceElevatorBaseStateDetail.BlockStateDetailKey) == null) return false;
                bindings = SpaceElevatorBindings(SpaceElevatorBaseInventoryComponent.SatelliteSlot, SpaceElevatorBaseInventoryComponent.GetCarriedPayloadItemIds(elevatorParam));
                return true;
            }

            // AppliesTo が両ブロックに絞るのでここへは来ない。来たら blockType とマスタの食い違いなので開発者へ出す
            // AppliesTo narrows to the two blocks, so reaching here means blockType and master disagree; tell the developer
            UnityEngine.Debug.LogError($"[SpecialInletBindingContributor] blockType は入れ口固定だがマスタの param が {param.GetType().Name}: Shift は取得待ちのまま");
            return false;
        }

        internal static IReadOnlyList<InletSlotBinding> SpaceElevatorBindings(int satelliteSlot, IReadOnlyList<ItemId> carriedPayloadItemIds)
        {
            return new[] { new InletSlotBinding(satelliteSlot, carriedPayloadItemIds) };
        }
    }
}
