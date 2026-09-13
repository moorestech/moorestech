using System;
using System.Collections.Generic;
using Client.Game.InGame.Playtest.Progress;

namespace Client.Tests.Playtest
{
    // プッシュされた内容だけを覚えるテスト用の記録先
    // A test sink that only remembers what was pushed
    public sealed class RecordingProgressSink : IPlaytestProgressSink
    {
        public readonly List<Guid> CraftedRecipes = new();
        public readonly List<string> SentReportKinds = new();

        public void RecordCraftExecuted(Guid recipeGuid)
        {
            CraftedRecipes.Add(recipeGuid);
        }

        public void RecordReportSent(string kind)
        {
            SentReportKinds.Add(kind);
        }
    }
}
