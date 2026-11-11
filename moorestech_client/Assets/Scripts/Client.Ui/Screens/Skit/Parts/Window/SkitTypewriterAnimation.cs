// S/Client.Ui/Screens/Skit/Parts/Window/SkitTypewriterAnimation.cs
using Client.Ui.Foundation.Motion.Core;
using Client.Ui.State.Skit.Logic;

namespace Client.Ui.Screens.Skit.Parts.Window
{
    /// <summary>
    /// 開始からの経過時間で出す字数を書く動き。全文が出たら終わる
    /// A motion that writes the shown character count from the elapsed time since start; finishes once everything is out
    /// </summary>
    public sealed class SkitTypewriterAnimation : IUiAnimation
    {
        private readonly SkitTypewriter _typewriter;
        private readonly int _intervalMs;
        private readonly int _totalCount;

        public SkitTypewriterAnimation(SkitTypewriter typewriter, int intervalMs, int totalCount)
        {
            _typewriter = typewriter;
            _intervalMs = intervalMs;
            _totalCount = totalCount;
        }

        public void Begin()
        {
            _typewriter.SetVisibleCount(0);
        }

        public bool Advance(float elapsedMilliseconds)
        {
            var count = SkitTypewriterRules.RevealCountAt(elapsedMilliseconds, _intervalMs, _totalCount);
            _typewriter.SetVisibleCount(count);
            return count >= _totalCount;
        }
    }
}
