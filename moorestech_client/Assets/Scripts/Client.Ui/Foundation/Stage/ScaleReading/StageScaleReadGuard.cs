using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage.ScaleReading
{
    /// <summary>
    /// 倍率が読めない状態を入口で 1 回知らせる
    /// Logs an unreadable scale once on entry
    /// </summary>
    internal sealed class StageScaleReadGuard
    {
        private bool _unreadableReported;

        // 読めたら旗を戻し false、入口だけ true
        // Readable clears flag (false); true only on entry
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
