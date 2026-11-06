using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.Input;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Client.WebUiHost.Health;
using Game.Paths;
using Newtonsoft.Json.Linq;
// WebのWS受信が始まるまで最大30秒待つ。未受信のまま書くと secondsSinceLastWebMessage が null になり検査にならない
// Wait up to 30s for the first web WS receipt; writing before it leaves secondsSinceLastWebMessage null and checks nothing
for (var i = 0; i < 60 && WebUiInputExclusivity.SecondsSinceLastWebMessage(System.Diagnostics.Stopwatch.GetTimestamp()) == null; i++) await Cysharp.Threading.Tasks.UniTask.Delay(500);
// 本番Writerで停止対象の報告を実outboxに作る
// Create an actual interruption target using the production writer
// webUiHealth は確保時の値を写すので、確保の代わりにここで本物の probe から読む
// webUiHealth is copied from the capture, so the real probe is read here in place of a capture
var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0UL, WebUiHealth = new WebUiHealthProbe().Read() };
var box = await writer.WriteAsync(data, "task10 stop during production upload", PlaytestReportKind.Bug);
if (!box.Ready) throw new System.Exception("READY failed");
// 箱の manifest に CEF 停止の記録の生の値 3 つが入っていること（manifest v6）
// The box's manifest carries the three raw CEF-stall values (manifest v6)
var boxManifest = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(box.BundleDirectory, "manifest.json")));
var webUiHealth = boxManifest["webUiHealth"];
var secondsSinceWeb = webUiHealth?["secondsSinceLastWebMessage"];
if ((int)boxManifest["schemaVersion"] != 6) throw new System.Exception("schemaVersion is not 6: " + boxManifest["schemaVersion"]);
if ((string)webUiHealth?["hostStartup"] != "started") throw new System.Exception("hostStartup is not started: " + webUiHealth);
if ((string)webUiHealth?["cefProcess"] != "observedAfterBrowserStart") throw new System.Exception("cefProcess is not observedAfterBrowserStart: " + webUiHealth);
if (secondsSinceWeb == null || secondsSinceWeb.Type == JTokenType.Null || (double)secondsSinceWeb < 0 || (double)secondsSinceWeb >= 15) throw new System.Exception("secondsSinceLastWebMessage is out of [0,15): " + webUiHealth);
// 本番送信のPendingを観測してから次のEditor更新で停止する
// Observe production Pending before stopping on the next Editor update
var runner = new PlaytestUploadRunner(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl), PlaytestOutboxDirectories.FromGameSystemPaths(), new PlaytestSteamTicketProvider());
runner.RequestUpload();
if (!(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryPending)) throw new System.Exception("Not pending before stop");
System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../.superpowers/sdd/task10-interrupted-id.txt")), box.BundleDirectory);
UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
return "pending upload interrupted: " + System.IO.Path.GetFileName(box.BundleDirectory);
