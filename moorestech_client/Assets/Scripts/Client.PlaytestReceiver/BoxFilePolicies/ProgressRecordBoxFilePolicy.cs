using Client.Game.InGame.Playtest.Progress.Storage;
using Client.PlaytestReceiver.Upload.Attempt;
using Client.PlaytestReceiver.Upload;

namespace Client.PlaytestReceiver.BoxFilePolicies
{
    // 進行記録の箱の格付け。記録本体は箱の唯一の中身なので必須、それ以外は補助
    // Ranks paths in a progress record box; the record itself is the box's whole point and is required, anything else supporting
    public sealed class ProgressRecordBoxFilePolicy : IPlaytestBoxFilePolicy
    {
        public bool CanResumeHold(string directory) => false;

        public System.Collections.Generic.IReadOnlyList<PlaytestOutboxBox> PrepareAttachments(PlaytestOutboxBox box) => System.Array.Empty<PlaytestOutboxBox>();
        public System.Collections.Generic.IReadOnlyList<string> ListPayloadFiles(string directory) => PlaytestOutboxScanner.ListPayloadFiles(directory);

        public string ValidateBeforeUpload(string boxDirectory, PlaytestBoxDeclaration declaration)
        {
            return null; // silent-ok: 進行記録にgit資料は無い / 必須本文は宣言が検査する
        }

        public PlaytestBundleFileRank RankOf(string relativePath)
        {
            return relativePath == ProgressRecordPaths.RecordFileName ? PlaytestBundleFileRank.Required : PlaytestBundleFileRank.Supporting;
        }
    }
}
