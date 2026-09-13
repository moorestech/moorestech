// 報告時点のワールドを起動し、報告者の位置から30秒観察して録画とスクショを残す
// Boot the reported world, observe 30 seconds from the reporter's position, keep the recording and screenshots
using Client.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine;

var bundle = @"__BUNDLE__";
var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
var player = manifest["clientState"]["playerPosition"];
var position = new Vector3((float)player["x"], (float)player["y"], (float)player["z"]);
var options = new PlaytestRunOptions { Record = true };

return PlaytestRunner.Run("bug-report-observe", options, async p =>
{
    await p.SkipOpeningSkitIfPlaying();
    p.Note($"報告者の位置へワープ: {position}");
    p.WarpPlayer(position);
    await p.WaitSeconds(1f);
    await p.Screenshot("start");
    p.Note((string)manifest["description"]);
    await p.WaitSeconds(15f);
    await p.Screenshot("mid");
    await p.WaitSeconds(15f);
    await p.Screenshot("end");
    p.Assert(true, "観察完了");
});
