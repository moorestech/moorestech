// Profiler記録を開始する（Editor自身は記録しない）
using UnityEditorInternal;
ProfilerDriver.ClearAllFrames();
ProfilerDriver.profileEditor = false;
ProfilerDriver.deepProfiling = false;
ProfilerDriver.enabled = true;
return "profiler started";
