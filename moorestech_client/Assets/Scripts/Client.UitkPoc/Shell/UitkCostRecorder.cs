using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Client.UitkPoc.Shell
{
    // UI Toolkitが1フレームに使うメインスレッド時間（更新＋再描画）をProfilerマーカーから読む。vSyncに隠れない計測値
    // Reads per-frame UI Toolkit main-thread time (update + repaint) from profiler markers, unaffected by vSync
    public class UitkCostRecorder
    {
        private static readonly string[] MarkerNames = { "PreLateUpdate.UIElementsUpdatePanels", "PostLateUpdate.UIElementsRepaintPanels" };

        private readonly List<ProfilerRecorder> _recorders = new();

        public UitkCostRecorder()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var markerName in MarkerNames)
            {
                var found = handles.FindIndex(h => ProfilerRecorderHandle.GetDescription(h).Name == markerName);
                // マーカー名がUnityの版で変わったら計測欄が0のままになるので、気づけるよう警告する
                // Warn when a marker is missing so a stuck-at-zero reading is noticed
                if (found < 0)
                {
                    Debug.LogWarning($"[UitkPoc] Profilerマーカーが見つからない: {markerName}");
                    continue;
                }
                var recorder = new ProfilerRecorder(handles[found], 1, ProfilerRecorderOptions.Default);
                recorder.Start();
                _recorders.Add(recorder);
            }
        }

        public float LastFrameMilliseconds()
        {
            var nanoseconds = 0L;
            foreach (var recorder in _recorders) nanoseconds += recorder.LastValue;
            return nanoseconds / 1_000_000f;
        }
    }
}
