// ベースライン複製ワールドを既定ワールドに差し替え、前回クラッシュ報告を閉じてローカル開始する
// 表示状態でfpsが大きく変わるため、画面スリープの抑止とEditorウィンドウ・Game ビューの前面化も行う
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.Reflection;
using System.Runtime.InteropServices;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
// ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED（cleanup.cs で解除）
SetThreadExecutionState(0x80000003u);
// 既に画面が消えていると上の抑止だけでは点かず約6fpsに間引かれるので、マウスを1px往復させて画面を起こす
// If the display is already off the flag above does not wake it (updates drop to ~6fps), so nudge the mouse 1px and back
mouse_event(0x0001, 1, 0, 0, System.UIntPtr.Zero);
mouse_event(0x0001, -1, 0, 0, System.UIntPtr.Zero);
var editorWindow = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
var wasMinimized = IsIconic(editorWindow);
if (wasMinimized) ShowWindow(editorWindow, 9);
// Game ビューが別タブの裏にあるか Editor が前面にないと PlayMode の更新が約6fpsに間引かれるので、両方を前に出す
// PlayMode updates drop to ~6fps when the Game view tab is hidden or the Editor is in the background
EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
SetForegroundWindow(editorWindow);
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
[DllImport("user32.dll")] static extern bool SetForegroundWindow(System.IntPtr hWnd);
[DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, System.UIntPtr extraInfo);
