using System.IO;
using Game.Paths;
using UnityEngine;

// 再生停止前に保存した箱を、次の再生後に同じIDで照合する
// Match the saved interrupted box by ID after the next play session
var saved = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.superpowers/sdd/task10-interrupted-id.txt"));
var box = File.ReadAllText(saved);
if (!File.Exists(Path.Combine(box, "READY"))) throw new System.Exception("Interrupted box lost READY");
if (!File.Exists(Path.Combine(box, "UPLOAD_DECLARED"))) throw new System.Exception("Interrupted box lost declaration");
if (!File.Exists(Path.Combine(box, "UPLOADED"))) throw new System.Exception("Interrupted box has not resumed");

// 実箱が残ったままであることを確認し、単なる箱不在を成功にしない
// Require the real progress box to remain instead of treating an absent box as success
var progress = Path.Combine(GameSystemPaths.ProgressRecordOutboxDirectory, "20261014_151419_783aca75");
if (!File.Exists(Path.Combine(progress, "READY"))) throw new System.Exception("Progress evidence box is absent");
if (File.Exists(Path.Combine(progress, "UPLOADED"))) throw new System.Exception("Developer route uploaded progress");
return "Resumed " + Path.GetFileName(box) + "; progress remains pending";
