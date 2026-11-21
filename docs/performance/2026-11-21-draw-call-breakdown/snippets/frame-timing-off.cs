// frame-timing-on.cs で入れた enableFrameTimingStats を、ProjectSettings.asset の値（0）へ戻す
// Play に入ると AppDomain が作り直されて元の値を覚えておけないので、リポジトリの値に固定で戻す
using UnityEditor;
PlayerSettings.enableFrameTimingStats = false;
return "enableFrameTimingStats=" + PlayerSettings.enableFrameTimingStats;
