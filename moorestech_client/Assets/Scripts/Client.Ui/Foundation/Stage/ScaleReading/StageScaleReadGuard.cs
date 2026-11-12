using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 倍率が読めない状態を、入ったとき 1 回だけ LogError で知らせ、読めたら旗を戻す読み口
    /// A read point that logs an unreadable scale once on entry and clears the flag once it is readable again
    /// </summary>
    internal sealed class StageScaleReadGuard
    {
        private bool _unreadableReported;

        // 読めたときは旗を戻して false、読めない間は入ったときだけ true
        // Clears the flag and returns false when readable; while unreadable returns true only on entry
        public bool ShouldReportUnreadable(bool readable)
        {
            if (readable)
            {
                _unreadableReported = false;
                return false;
            }
            if (_unreadableReported) return false;
            _unreadableReported = true;
            return true;
        }

        public bool TryRead(IPanel panel, string owner, string consequence, out float scale)
        {
            var readable = StageScaleMeasure.TryRead(panel, out scale);
            if (ShouldReportUnreadable(readable)) Debug.LogError($"[{owner}] stage scale is unreadable; {consequence}");
            return readable;
        }
    }
}
