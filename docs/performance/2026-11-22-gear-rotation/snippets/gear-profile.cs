// Profiler の記録を始める。6秒ほど待ってから gear-profile-collect.cs で回収する
using UnityEditorInternal;
ProfilerDriver.ClearAllFrames();
ProfilerDriver.profileEditor = false;
ProfilerDriver.deepProfiling = false;
ProfilerDriver.enabled = true;
return "profiler started";
