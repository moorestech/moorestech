using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps.V7ToV8
{
    /// <summary>
    /// 版7→8の変換部品。全部品の検査が通ってから適用するので、どれかが拒否してもセーブは書き換わらない
    /// A version 7-to-8 conversion part; parts apply only after every part passes inspection, so a refusal never leaves the save half-rewritten
    /// </summary>
    public interface IV7ToV8MigrationPart
    {
        // 書き換える対象を集める。辿れない形なら理由を返し、セーブには触れない
        // Collect what to rewrite; on an unwalkable shape return the reason without touching the save
        bool TryPrepare(JObject save, out string failureReason);

        // 検査で集めた対象だけを書き換え、ログに載せる要約を返す
        // Rewrite only what inspection collected and return a summary for the log
        string ApplyAndSummarize();
    }
}
