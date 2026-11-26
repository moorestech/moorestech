// 既定ワールドの差し替えを解除し、Profiler を止める（Mac 用。benchmark/snippets/cleanup.cs の Windows 専用部分を除いたもの）
Game.Paths.GameSystemPaths.SetDefaultWorldDirectoryOverride(null);
UnityEditorInternal.ProfilerDriver.enabled = false;
UnityEditorInternal.ProfilerDriver.ClearAllFrames();
return "override cleared: " + Game.Paths.GameSystemPaths.DefaultWorldDirectory;
