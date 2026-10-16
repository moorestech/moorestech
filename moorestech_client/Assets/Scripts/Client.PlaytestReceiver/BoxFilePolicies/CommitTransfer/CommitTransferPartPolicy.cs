using System;
using System.Collections.Generic;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.BoxFilePolicies.CommitTransfer
{
    internal sealed class CommitTransferPartPolicy : IPlaytestBoxFilePolicy
    {
        public bool CanResumeHold(string directory) => false;

        public IReadOnlyList<PlaytestOutboxBox> PrepareAttachments(PlaytestOutboxBox box) => Array.Empty<PlaytestOutboxBox>();
        public IReadOnlyList<string> ListPayloadFiles(string directory) => PlaytestOutboxScanner.ListPayloadFiles(directory);
        public PlaytestBundleFileRank RankOf(string relativePath) => PlaytestBundleFileRank.Required;
        public string ValidateBeforeUpload(string directory, PlaytestBoxDeclaration declaration)
        {
            return declaration.Files.Count == 1 && declaration.Files[0].Path == "chunk" && declaration.Skipped.Count == 0
                ? null : "Commit transfer requires exactly one intact chunk";
        }
    }
}
