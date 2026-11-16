using System.IO;
using Client.Playtest;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Playtest
{
    /// <summary>
    /// 起動の応答「already playing」の定数と run-scenario.sh の比較文字列が同じであることを固定する（L0）
    /// Pins that the "already playing" boot reply constant matches the string run-scenario.sh compares against (L0)
    /// </summary>
    public class PlaytestAlreadyPlayingReplyTest
    {
        private const string RunScenarioScriptPath = ".agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh";

        [Test]
        public void run_scenarioは定数と同じ文字列で再送の応答を見分ける()
        {
            // シェルは完全一致で比べるので、引用符で囲んだ形のまま含まれていること
            // The shell compares by exact match, so the quoted form must appear verbatim
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var script = File.ReadAllText(Path.Combine(repoRoot, RunScenarioScriptPath));
            var quotedReply = $"\"{PlaytestBoot.AlreadyPlayingReply}\"";
            StringAssert.Contains($"== {quotedReply} ]]", script, $"run-scenario.sh が {quotedReply} と完全一致で比べていない（PlaytestBoot.AlreadyPlayingReply とずれた）");
        }
    }
}
