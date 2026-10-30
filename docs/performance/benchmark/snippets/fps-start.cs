// Profilerもフックも無しのfps計測窓を開始する
System.AppDomain.CurrentDomain.SetData("bench_fps_frame", UnityEngine.Time.frameCount);
System.AppDomain.CurrentDomain.SetData("bench_fps_time", UnityEngine.Time.realtimeSinceStartupAsDouble);
return "fps window started";
