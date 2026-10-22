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
        private float _windowUitkTotal;
        private float _windowUitkWorst;
        private readonly UitkCostRecorder _uitkCost = new();

        public float LastAverageMs { get; private set; }
        public float LastWorstMs { get; private set; }
        public float LastUitkAverageMs { get; private set; }
        public float LastUitkWorstMs { get; private set; }

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
            var uitkMs = _uitkCost.LastFrameMilliseconds();
            _windowUitkTotal += uitkMs;
            _windowUitkWorst = Mathf.Max(_windowUitkWorst, uitkMs);
            if (_windowElapsed < 1f) return false;

            // 1秒窓を閉じて表示を更新する
            // Close the one-second window and refresh the badge
            LastAverageMs = _windowElapsed / _windowFrames * 1000f;
            LastWorstMs = _windowWorst * 1000f;
            LastUitkAverageMs = _windowUitkTotal / _windowFrames;
            LastUitkWorstMs = _windowUitkWorst;
            Badge.text = $"{_windowFrames / _windowElapsed:0} fps  frame {LastAverageMs:0.0}ms  UITK {LastUitkAverageMs:0.00}ms(max {LastUitkWorstMs:0.0})  VE {lastElementCount}";
            _windowUitkTotal = 0;
            _windowUitkWorst = 0;
            _windowElapsed = 0;
            _windowFrames = 0;
            _windowWorst = 0;
            return true;
        }
    }
}
