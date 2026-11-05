// fps計測窓を閉じてJSONで返す
var dom = System.AppDomain.CurrentDomain;
int frames = UnityEngine.Time.frameCount - (int)dom.GetData("bench_fps_frame");
double secs = UnityEngine.Time.realtimeSinceStartupAsDouble - (double)dom.GetData("bench_fps_time");
var ci = System.Globalization.CultureInfo.InvariantCulture;
return "{\"frames\":" + frames + ",\"seconds\":" + secs.ToString("F3", ci) + ",\"fps\":" + (frames / secs).ToString("F3", ci) + ",\"frameMsAvg\":" + (secs * 1000 / frames).ToString("F3", ci) + "}";
