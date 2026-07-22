using Client.Game.InGame.Block;
using Client.Input;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     Ctrl+Zで直前の建築操作を取り消す。具体的な取り消し処理は各レコードに委譲する
    ///     Undo the latest build operation on Ctrl+Z; the concrete undo logic is delegated to each record
    /// </summary>
    public class BuildUndoService
    {
        private readonly BuildOperationHistory _history;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private bool _isUndoing;

        public BuildUndoService(BuildOperationHistory history, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _history = history;
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public void ManualUpdate()
        {
            if (!IsUndoKeyPressed()) return;
            if (_isUndoing) return;
            if (!_history.TryPop(out var record)) return;

            UndoAsync(record).Forget();

            #region Internal

            static bool IsUndoKeyPressed()
            {
                var modifierHeld = HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);
                return modifierHeld && HybridInput.GetKeyDown(KeyCode.Z);
            }

            #endregion
        }

        private async UniTask UndoAsync(IBuildOperationRecord record)
        {
            _isUndoing = true;
            // ネットワーク送受信（外部境界）の例外でも再入フラグを必ず復帰させる（try-catch原則禁止の境界例外条項）
            // Guarantee the re-entrancy flag resets even on network-boundary exceptions (boundary exemption of the no-try-catch rule)
            try
            {
                await record.UndoAsync(_blockGameObjectDataStore);
            }
            finally
            {
                _isUndoing = false;
            }
        }
    }
}
