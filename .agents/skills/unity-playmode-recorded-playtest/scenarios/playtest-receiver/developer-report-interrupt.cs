using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Game.Paths;
using Newtonsoft.Json.Linq;
// 本番Writerで停止対象の報告を実outboxに作る
// Create an actual interruption target using the production writer
var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason), new GameSystemBugReportOutboxRoot());
var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0UL };
var box = await writer.WriteAsync(data, "task10 stop during production upload", PlaytestReportKind.Bug);
if (!box.Ready) throw new System.Exception("READY failed");
// 箱の manifest は v8 で webUiHealth を持たない。確保データを手で組むので UI の例外は数えておらず、不明で出る（A-244）
// The box's manifest is v8 without webUiHealth; the captured data is hand-built, so UI exceptions are uncounted and read unknown (A-244)
var boxManifest = JObject.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(box.BundleDirectory, "manifest.json")));
if ((int)boxManifest["schemaVersion"] != 8) throw new System.Exception("schemaVersion is not 8: " + boxManifest["schemaVersion"]);
if (boxManifest["webUiHealth"] != null) throw new System.Exception("webUiHealth is still in a v8 manifest: " + boxManifest["webUiHealth"]);
if ((string)boxManifest["gameUiExceptions"]?["status"] != "unknown") throw new System.Exception("gameUiExceptions is not unknown for hand-built capture data: " + boxManifest["gameUiExceptions"]);
// 本番送信のPendingを観測してから次のEditor更新で停止する
// Observe production Pending before stopping on the next Editor update
var runner = new PlaytestUploadRunner(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl), PlaytestOutboxDirectories.FromGameSystemPaths(), new PlaytestSteamTicketProvider());
runner.RequestUpload();
if (!(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryPending)) throw new System.Exception("Not pending before stop");
System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../.superpowers/sdd/task10-interrupted-id.txt")), box.BundleDirectory);
UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
return "pending upload interrupted: " + System.IO.Path.GetFileName(box.BundleDirectory);
