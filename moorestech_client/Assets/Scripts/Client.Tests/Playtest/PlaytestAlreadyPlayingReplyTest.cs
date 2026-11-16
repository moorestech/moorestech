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
        private const string WinunityScriptPath = ".agents/skills/unity-playmode-recorded-playtest/scenarios/uitk/stage5/driver/winunity-scenario-run.sh";

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

        [Test]
        public void winunityの台本は定数を含む部分一致で再送の応答を見分ける()
        {
            // winunity 側は部分一致で比べるので、定数がその部分文字列を含み続けること
            // The winunity script matches by substring, so the constant must keep containing it
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var script = File.ReadAllText(Path.Combine(repoRoot, WinunityScriptPath));
            StringAssert.Contains("*'already playing'*", script, "winunity-scenario-run.sh の部分一致の書式が変わった");
            StringAssert.Contains("already playing", PlaytestBoot.AlreadyPlayingReply, "PlaytestBoot.AlreadyPlayingReply が winunity の部分一致文字列を含まなくなった");
        }
    }
}
