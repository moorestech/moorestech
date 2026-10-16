using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    internal sealed class PlaytestUploadAttachments
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestSession _session;

        public PlaytestUploadAttachments(IPlaytestReceiverApi api, PlaytestSession session)
        {
            _api = api;
            _session = session;
        }

        internal async UniTask<PlaytestUploadAttemptFailure> RunAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            foreach (var attachment in box.FilePolicy.PrepareAttachments(box))
            {
                var declaration = PlaytestBoxDeclaration.Build(attachment);
                var invalid = declaration.MissingRequiredReason ?? attachment.FilePolicy.ValidateBeforeUpload(attachment.Directory, declaration);
                if (invalid != null) throw new IOException(invalid);
                // 親が公開される前に全添付を配送し、失敗は親の再試行へ返す
                // Ship every attachment before publishing the parent; failures enter the parent's retry policy
                var failure = await new PlaytestUploadAttempt(_api, _session).RunAsync(attachment, declaration, new HashSet<string>(), token);
                if (failure != null) return failure.ForAttachment(attachment.BundleId);
            }
            return null;
        }
    }
}
