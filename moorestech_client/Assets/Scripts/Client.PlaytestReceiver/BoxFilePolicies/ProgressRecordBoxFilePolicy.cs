using Client.Game.InGame.Playtest.Progress.Storage;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.BoxFilePolicies
{
    // 進行記録の箱の格付け。記録本体は箱の唯一の中身なので必須、それ以外は補助
    // Ranks paths in a progress record box; the record itself is the box's whole point and is required, anything else supporting
    public sealed class ProgressRecordBoxFilePolicy : IPlaytestBoxFilePolicy
    {
        public PlaytestBundleFileRank RankOf(string relativePath)
        {
            return relativePath == ProgressRecordPaths.RecordFileName ? PlaytestBundleFileRank.Required : PlaytestBundleFileRank.Supporting;
        }
    }
}
