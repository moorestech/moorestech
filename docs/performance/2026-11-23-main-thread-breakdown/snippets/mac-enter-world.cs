// Mac 用：run-mac.sh が /tmp/moorestech-perf-world へ複製したワールドを既定ワールドに差し替え、前回クラッシュ報告を閉じてローカル開始する（2026-11-19 の mac-enter-world.cs の置き場を変えたもの）
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
var bf = BindingFlags.NonPublic | BindingFlags.Instance;
Game.Paths.GameSystemPaths.SetDefaultWorldDirectoryOverride("/tmp/moorestech-perf-world");
var popup = Object.FindFirstObjectByType<Client.MainMenu.Playtest.CrashReportPopup>(FindObjectsInactive.Include);
if (popup && popup.gameObject.activeInHierarchy)
  ((Button)typeof(Client.MainMenu.Playtest.CrashReportPopup).GetField("skipButton", bf).GetValue(popup)).onClick.Invoke();
var start = Object.FindFirstObjectByType<Client.MainMenu.StartLocal>(FindObjectsInactive.Include);
if (start == null) return "not-ready: title scene not loaded";
var verdict = Client.Starter.Playtest.TitleGates.PlaytestTitleGates.EvaluateStart("Benchmark", out _);
if (verdict != Client.Starter.Playtest.TitleGates.PlaytestStartVerdict.Passed) return "not-ready: " + verdict;
((Button)typeof(Client.MainMenu.StartLocal).GetField("startLocalButton", bf).GetValue(start)).onClick.Invoke();
return "started world=" + Game.Paths.GameSystemPaths.DefaultWorldDirectory;
