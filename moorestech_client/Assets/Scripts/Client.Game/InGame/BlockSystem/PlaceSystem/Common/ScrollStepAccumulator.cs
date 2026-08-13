namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common
{
    /// <summary>
    /// 連続値のスクロール量を整数ステップへ丸める蓄積器。トラックパッドの微小デルタを取りこぼさず、
    /// 逆回しは1ノッチで必ず1段戻す。
    /// Accumulator turning continuous scroll deltas into whole steps: it keeps fractional trackpad
    /// deltas and guarantees that one reverse notch always steps back once.
    /// </summary>
    public class ScrollStepAccumulator
    {
        private float _accumulated;

        public int Accumulate(float scroll)
        {
            // 逆回しの直前に順方向の端数が残っていると切り捨てで0段になり無反応に見えるため、反転時に捨てる
            // A leftover forward remainder would truncate a reverse notch to zero steps and look unresponsive, so drop it on reversal
            if (scroll * _accumulated < 0f) _accumulated = 0f;

            _accumulated += scroll;
            var step = (int)_accumulated;
            _accumulated -= step;
            return step;
        }

        public void Reset()
        {
            _accumulated = 0f;
        }
    }
}
