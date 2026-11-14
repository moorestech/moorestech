using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Client.Playtest.Operations
{
    // UI のパネル更新と描画のマーカーを N フレーム読んだ結果（ms）
    // The UI panel update and repaint markers read over N frames (ms)
    public readonly struct PlaytestPanelCost
    {
        public readonly int Frames;
        public readonly double MeanMs;
        public readonly double P95Ms;
        public readonly double MaxMs;
        public readonly string Markers;

        public PlaytestPanelCost(int frames, double meanMs, double p95Ms, double maxMs, string markers)
        {
            Frames = frames;
            MeanMs = meanMs;
            P95Ms = p95Ms;
            MaxMs = maxMs;
            Markers = markers;
        }
    }

    // パネル更新と描画の時間を測る
    // Measures panel update and repaint time
    public static class PlaytestPanelCostRecorder
    {
        // パネル更新と描画のマーカー
        // Panel update and repaint markers
        private static readonly string[] MarkerKeywords = { "UIElementsUpdatePanels", "UIElementsRepaintPanels" };

        public static IReadOnlyList<ProfilerRecorderDescription> FindPanelMarkers()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            return handles
                .Select(ProfilerRecorderHandle.GetDescription)
                .Where(d => MarkerKeywords.Any(k => d.Name.Contains(k)))
                .ToList();
        }

        public static IReadOnlyList<string> ListUiElementsMarkerNames()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            return handles.Select(h => ProfilerRecorderHandle.GetDescription(h).Name).Where(n => n.Contains("UIElements")).OrderBy(n => n).ToList();
        }

        public static async UniTask<PlaytestPanelCost> Measure(IReadOnlyList<ProfilerRecorderDescription> markers, int frames)
        {
            // 前フレームのマーカー和を読む
            // Read the previous frame's marker sum
            var recorders = markers.Select(d => ProfilerRecorder.StartNew(d.Category, d.Name, 1)).ToList();
            var samplesMs = new List<double>(frames);
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            for (var i = 0; i < frames; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                samplesMs.Add(recorders.Sum(r => r.LastValue) / 1_000_000.0);
            }
            foreach (var recorder in recorders) recorder.Dispose();

            var sorted = samplesMs.OrderBy(v => v).ToList();
            var p95 = sorted[(int)System.Math.Ceiling(sorted.Count * 0.95) - 1];
            return new PlaytestPanelCost(frames, samplesMs.Average(), p95, sorted[^1], string.Join("+", markers.Select(d => d.Name)));
        }
    }
}
