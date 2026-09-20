using System;
using Core.Master;
using UniRx;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    ///     設置高さオフセットの唯一の正。通常設置・ベルトの実オフセットとHUD表示は全てここを読む
    ///     高さを0へ戻す規則（持ち替え）もここが持ち、設置系ごとに散らさない
    ///     The single source of truth for the placement height offset; placement systems and the HUD all read it
    ///     The rule that returns the height to ground (a block switch) lives here too, not in each place system
    /// </summary>
    public class PlacementHeightOffset
    {
        private readonly ReactiveProperty<int> _value = new(0);
        private BlockId? _previousSelectedBlockId;

        public int Value => _value.Value;
        public IObservable<int> OnChanged => _value;

        // 持ち替えたら高さは地表基準へ戻す。戻り値は選択が変わったか
        // A block switch returns the height to ground level; the return says whether the selection changed
        public bool SyncSelectedBlock(BlockId blockId)
        {
            var isChanged = _previousSelectedBlockId != blockId;
            _previousSelectedBlockId = blockId;

            if (isChanged) _value.Value = 0;
            return isChanged;
        }

        // 高さを扱わない設置系へ移ったときに地表基準へ戻す。次の持ち替え判定も初期化する
        // Returns to ground level when moving to a system that has no height, resetting the next block-switch check too
        public void ResetToGround()
        {
            _previousSelectedBlockId = null;
            _value.Value = 0;
        }

        // 高さを相対に動かす入口。入力の解釈と値の保持を分ける
        // An entry that moves the height relatively, keeping input interpretation apart from the stored value
        public void Adjust(int delta)
        {
            _value.Value += delta;
        }

        // ドラッグ開始時の高さへ戻す。復元はドラッグ所有者だけが呼ぶ
        // Restores the height captured at drag start; only the drag owner calls it
        internal void Restore(int value)
        {
            _value.Value = value;
        }
    }
}
