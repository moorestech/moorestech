using UnityEngine;

namespace Game.Block.Blocks.Util
{
    /// <summary>
    ///     配信する供給電力（分子）と要求電力（分母）を1点で一括確定した値。片方だけの書き換えを型で防ぐ
    ///     The published supply (numerator) and request (denominator) latched together at one point; the type prevents rewriting only one side
    /// </summary>
    public readonly struct PublishedPowerLatch
    {
        public readonly float CurrentPower;
        public readonly float RequestPower;

        public PublishedPowerLatch(float currentPower, float requestPower)
        {
            CurrentPower = currentPower;
            RequestPower = requestPower;
        }

        // 前回の確定値から分子・分母のどちらかが動いたか。配信値の変化を発火条件にするために使う
        // Whether either side moved from the previous latch; used to fire when a published value changed
        public bool MovedFrom(PublishedPowerLatch previous)
        {
            return !Mathf.Approximately(previous.CurrentPower, CurrentPower) || !Mathf.Approximately(previous.RequestPower, RequestPower);
        }
    }
}
