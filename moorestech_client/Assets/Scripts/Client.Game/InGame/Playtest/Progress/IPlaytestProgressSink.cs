using System;

namespace Client.Game.InGame.Playtest.Progress
{
    // 購読では取れない「操作そのもの」を記録側へプッシュする窓口。実装は ProgressRecorder 1つ
    // The window that pushes actions themselves, which no subscription observes; ProgressRecorder is the only implementation
    public interface IPlaytestProgressSink
    {
        // クラフトは送信のみで応答が無い。成立したかは分からないので「要求した」までを記録する
        // A craft is send-only with no response, so only the request — never its success — is recorded
        void RecordCraftRequested(Guid recipeGuid);

        void RecordReportSent(string kind);
    }
}
