// 既定ワールドの差し替えを解除する（Editorプロセスに残すと以後の通常プレイが作業用ワールドを読む）
Game.Paths.GameSystemPaths.SetDefaultWorldDirectoryOverride(null);
// display-guard.cs で出した画面スリープ抑止を解除する（ES_CONTINUOUS のみ）
SetThreadExecutionState(0x80000000u);
UnityEditorInternal.ProfilerDriver.enabled = false;
return "override cleared: " + Game.Paths.GameSystemPaths.DefaultWorldDirectory;


[System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
