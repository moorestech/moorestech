using System;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Confirm
{
    /// <summary>
    ///     F長押しの満了時間を選択件数から3段で決める（1〜4件 0.75秒 / 5〜19件 1.25秒 / 20件以上 1.75秒）
    ///     Hold-to-confirm duration in three tiers by target count (1-4: 0.75s / 5-19: 1.25s / 20+: 1.75s)
    /// </summary>
    public static class DeleteHoldDuration
    {
        private const int MediumTierMinCount = 5;
        private const int LargeTierMinCount = 20;
        private const float SmallTierSeconds = 0.75f;
        private const float MediumTierSeconds = 1.25f;
        private const float LargeTierSeconds = 1.75f;

        public static float SecondsFor(int targetCount)
        {
            // 対象なしの長押しは進捗を始めないので0件はここに来ない
            // A hold without targets never starts progress, so zero never reaches here
            if (targetCount < 1) throw new ArgumentOutOfRangeException(nameof(targetCount), targetCount, "hold duration needs at least one target");
            if (LargeTierMinCount <= targetCount) return LargeTierSeconds;
            if (MediumTierMinCount <= targetCount) return MediumTierSeconds;
            return SmallTierSeconds;
        }
    }
}
