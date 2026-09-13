using System;
using System.Collections.Generic;
using Game.Research;

namespace Client.Game.InGame.Playtest.Progress
{
    // 初期ハンドシェイクから「セッション開始時点で既に到達していた分」を作る。以後はイベントで足す
    // Builds what was already reached at session start from the initial handshake; events add to it afterwards
    public static class ProgressBaseline
    {
        public static List<string> CompletedChallengeGuids(IEnumerable<Guid> completedChallenges)
        {
            var result = new List<string>();
            foreach (var guid in completedChallenges)
            {
                var text = guid.ToString();
                if (!result.Contains(text)) result.Add(text);
            }
            return result;
        }

        public static List<string> CompletedResearchGuids(IEnumerable<KeyValuePair<Guid, ResearchNodeState>> researchStates)
        {
            var result = new List<string>();
            foreach (var state in researchStates)
            {
                if (state.Value != ResearchNodeState.Completed) continue;
                var text = state.Key.ToString();
                if (!result.Contains(text)) result.Add(text);
            }
            return result;
        }
    }
}
