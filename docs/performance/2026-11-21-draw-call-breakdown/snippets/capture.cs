// Game ビューを PNG に保存する（GRD の有無で見た目が変わっていないかを比べる）。書き出しは次のフレームなので、呼んだ後に数秒待ってから取りに行く
ScreenCapture.CaptureScreenshot("C:/moorestech-perf/shot.png");
return "capture requested";
