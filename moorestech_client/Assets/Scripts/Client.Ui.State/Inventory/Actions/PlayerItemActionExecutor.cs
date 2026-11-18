using System.Collections.Generic;
using Client.Ui.State.Commands;

namespace Client.Ui.State.Inventory.Actions
{
    /// <summary>
    /// プレイヤー側の行為を順に実行し、最初の拒否で止めてそれを返す（Web は 1 行為ずつ送り、失敗をその場で返す）
    /// Runs player-side actions in order and stops at the first rejection, returning it (Web sends one action at a time and fails on the spot)
    /// </summary>
    public sealed class PlayerItemActionExecutor
    {
        private readonly IPlayerInventoryOperations _operations;

        public PlayerItemActionExecutor(IPlayerInventoryOperations operations)
        {
            _operations = operations;
        }

        public UiCommandResult Execute(IReadOnlyList<PlayerItemAction> actions)
        {
            foreach (var action in actions)
            {
                var result = action.Run(_operations);
                if (result is UiCommandRejected) return result;
            }
            return UiCommandAccepted.Instance;
        }
    }
}
