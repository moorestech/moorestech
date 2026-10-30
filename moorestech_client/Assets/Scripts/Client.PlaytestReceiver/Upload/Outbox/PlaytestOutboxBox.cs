using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload.Attempt;

namespace Client.PlaytestReceiver.Upload
{
    // 送る単位。生成時に必須値を受け、以後は書き換えない
    // One shippable unit; required values are taken at construction and never change afterwards
    public sealed class PlaytestOutboxBox
    {
        public readonly string Directory;
        public readonly string BundleId;
        public readonly PlaytestUploadKind Kind;

        // 箱の中のパスの格付け。outboxごとに合成ルートが対にしたものを受け継ぐ
        // How paths inside the box rank; inherited from the pairing the composition root made per outbox
        public readonly IPlaytestBoxFilePolicy FilePolicy;

        public PlaytestOutboxBox(string directory, string bundleId, PlaytestUploadKind kind, IPlaytestBoxFilePolicy filePolicy)
        {
            Directory = directory;
            BundleId = bundleId;
            Kind = kind;
            FilePolicy = filePolicy;
        }
    }
}
