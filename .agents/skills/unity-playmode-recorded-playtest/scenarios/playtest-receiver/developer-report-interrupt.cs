using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Capture;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Game.Paths;
// 本番Writerで停止対象の報告を実outboxに作る
// Create an actual interruption target using the production writer
var writer = new BugReportBundleWriter(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
var data = new BugReportCapturedData { CaptureId = 1, ReportTick = 0UL };
var box = await writer.WriteAsync(data, "task10 stop during production upload", PlaytestReportKind.Bug);
if (!box.Ready) throw new System.Exception("READY failed");
// 本番送信のPendingを観測してから次のEditor更新で停止する
// Observe production Pending before stopping on the next Editor update
var runner = new PlaytestUploadRunner(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl), PlaytestOutboxDirectories.FromGameSystemPaths(), new PlaytestSteamTicketProvider());
runner.RequestUpload();
if (!(PlaytestDeliveryStatusBoard.Current.Value is PlaytestDeliveryPending)) throw new System.Exception("Not pending before stop");
System.IO.File.WriteAllText(System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../.superpowers/sdd/task10-interrupted-id.txt")), box.BundleDirectory);
UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
return "pending upload interrupted: " + System.IO.Path.GetFileName(box.BundleDirectory);
