// Profiler の記録を止めて溜まったフレームを捨て、開いている Profiler ウィンドウを閉じる（記録し続けるとこのワールドではメモリが数十GB に膨らむ）
UnityEditorInternal.ProfilerDriver.enabled = false;
UnityEditorInternal.ProfilerDriver.ClearAllFrames();
var closed = 0;
var type = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.ProfilerWindow");
foreach (var w in UnityEngine.Resources.FindObjectsOfTypeAll(type)) { ((UnityEditor.EditorWindow)w).Close(); closed++; }
return "profiler enabled=" + UnityEditorInternal.ProfilerDriver.enabled + " windowsClosed=" + closed;
