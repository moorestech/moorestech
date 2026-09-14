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
            var texts = new List<string>();
            foreach (var guid in completedChallenges) texts.Add(guid.ToString());
            return Distinct(texts);
        }

        public static List<string> CompletedResearchGuids(IEnumerable<KeyValuePair<Guid, ResearchNodeState>> researchStates)
        {
            var texts = new List<string>();
            foreach (var state in researchStates)
            {
                if (state.Value != ResearchNodeState.Completed) continue;
                texts.Add(state.Key.ToString());
            }
            return Distinct(texts);
        }

        // 重複判定は集合で行う。List.Contains の線形探索を件数ぶん繰り返すと、到達数が増えるほど起動が重くなる
        // Duplicates are decided by a set; repeating List.Contains per element makes the boot heavier as the reached count grows
        private static List<string> Distinct(List<string> texts)
        {
            var seen = new HashSet<string>();
            var result = new List<string>(texts.Count);
            foreach (var text in texts)
                if (seen.Add(text))
                    result.Add(text);
            return result;
        }
    }
}
