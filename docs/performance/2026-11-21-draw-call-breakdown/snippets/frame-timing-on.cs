// Play 前に FrameTimingManager を使えるようにする（ProjectSettings には保存しない。後片付けで frame-timing-off.cs が戻す）
using UnityEditor;
PlayerSettings.enableFrameTimingStats = true;
return "enableFrameTimingStats=" + PlayerSettings.enableFrameTimingStats;
