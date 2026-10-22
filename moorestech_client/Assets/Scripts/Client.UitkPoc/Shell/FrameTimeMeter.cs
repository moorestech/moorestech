using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Shell
{
    // 直近1秒のフレーム時間を集計して右上に出す
    // Aggregates the last second of frame times into the top-right badge
    public class FrameTimeMeter
    {
        public readonly Label Badge;
        private float _windowElapsed;
        private int _windowFrames;
        private float _windowWorst;

        public float LastAverageMs { get; private set; }
        public float LastWorstMs { get; private set; }

        public FrameTimeMeter()
        {
            Badge = new Label("-- fps");
            Badge.AddToClassList("poc-frame-meter");
            Badge.pickingMode = PickingMode.Ignore;
        }

        // 1秒窓が閉じたフレームだけtrueを返し、呼び出し側に要素数の再集計を任せる
        // Returns true only when the one-second window closes so the caller recounts elements
        public bool AddFrame(float deltaSeconds, int lastElementCount)
        {
            _windowElapsed += deltaSeconds;
            _windowFrames++;
            _windowWorst = Mathf.Max(_windowWorst, deltaSeconds);
            if (_windowElapsed < 1f) return false;

            // 1秒窓を閉じて表示を更新する
            // Close the one-second window and refresh the badge
            LastAverageMs = _windowElapsed / _windowFrames * 1000f;
            LastWorstMs = _windowWorst * 1000f;
            Badge.text = $"{_windowFrames / _windowElapsed:0} fps  avg {LastAverageMs:0.0}ms  worst {LastWorstMs:0.0}ms  VE {lastElementCount}";
            _windowElapsed = 0;
            _windowFrames = 0;
            _windowWorst = 0;
            return true;
        }
    }
}
