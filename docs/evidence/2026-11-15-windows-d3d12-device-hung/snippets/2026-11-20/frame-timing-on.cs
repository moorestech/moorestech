// Play 前に FrameTimingManager を使えるようにする（ProjectSettings には保存しない）
using UnityEditor;
PlayerSettings.enableFrameTimingStats = true;
return "enableFrameTimingStats=" + PlayerSettings.enableFrameTimingStats;
