using System;
using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.ItemMove;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Ui.State.SlotCommands
{
    /// <summary>
    /// 計画の列を順に実行する。プレイヤー側へ写せればプレイヤー側、ブロック側へ写せればブロック側。最初の拒否で止める
    /// Runs a plan list in order: player side if convertible, block side if convertible; stops at the first rejection
    /// </summary>
    public sealed class PlannedItemActionRunner
    {
        private readonly PlayerItemActionExecutor _player;
        private readonly BlockItemActionExecutor _block;

        public PlannedItemActionRunner(PlayerItemActionExecutor player, BlockItemActionExecutor block)
        {
            _player = player;
            _block = block;
        }

        public UiCommandResult Execute(IReadOnlyList<PlannedItemAction> actions)
        {
            foreach (var action in actions)
            {
                var result = Run(action);
                if (result is UiCommandRejected) return result;
            }
            return UiCommandAccepted.Instance;

            #region Internal

            // プレイヤーへ写す → ブロックへ写す。どちらも不可は計画器の誤りなので止める（拒否の理由へ相乗りさせない。D-2）
            // Player first, then block; neither is a planner bug, so stop (never piggyback on a reject reason; D-2)
            UiCommandResult Run(PlannedItemAction action)
            {
                if (PlayerItemActionConversion.TryConvert(action, out var playerAction)) return _player.Execute(new[] { playerAction });
                if (BlockItemActionConversion.TryConvert(action, out var blockAction)) return _block.Execute(blockAction);
                throw new InvalidOperationException($"[PlannedItemActionRunner] plan has no executor: {action}");
            }

            #endregion
        }
    }
}
