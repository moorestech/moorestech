// ベースライン複製ワールドを既定ワールドに差し替え、前回クラッシュ報告を閉じてローカル開始する
// 表示状態でfpsが大きく変わるため、画面スリープの抑止とEditorウィンドウの通常表示化も行う
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.Runtime.InteropServices;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
// ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED（cleanup.cs で解除）
SetThreadExecutionState(0x80000003u);
var editorWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
var wasMinimized = IsIconic(editorWindow);
if (wasMinimized) ShowWindow(editorWindow, 9);
Game.Paths.GameSystemPaths.SetDefaultWorldDirectoryOverride(@"C:\moorestech-perf\work\current");
var popup = Object.FindFirstObjectByType<Client.MainMenu.Playtest.CrashReportPopup>(FindObjectsInactive.Include);
if (popup && popup.gameObject.activeInHierarchy)
  ((Button)typeof(Client.MainMenu.Playtest.CrashReportPopup).GetField("skipButton", bf).GetValue(popup)).onClick.Invoke();
// タイトルの準備（同意・クラッシュ確認ゲート）が済むまでは押さずに not-ready を返し、呼び出し側が再試行する
var start = Object.FindFirstObjectByType<Client.MainMenu.StartLocal>(FindObjectsInactive.Include);
if (start == null) return "not-ready: title scene not loaded";
var verdict = Client.Starter.Playtest.TitleGates.PlaytestTitleGates.EvaluateStart("Benchmark", out _);
if (verdict != Client.Starter.Playtest.TitleGates.PlaytestStartVerdict.Passed) return "not-ready: " + verdict;
((Button)typeof(Client.MainMenu.StartLocal).GetField("startLocalButton", bf).GetValue(start)).onClick.Invoke();
return "started world=" + Game.Paths.GameSystemPaths.DefaultWorldDirectory + " wasMinimized=" + wasMinimized;


[DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
[DllImport("user32.dll")] static extern bool IsIconic(System.IntPtr hWnd);
[DllImport("user32.dll")] static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
