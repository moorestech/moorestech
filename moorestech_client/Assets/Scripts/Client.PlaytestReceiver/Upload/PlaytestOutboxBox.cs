using Client.PlaytestReceiver.Http;

namespace Client.PlaytestReceiver.Upload
{
    // 送る単位。生成時に必須値を受け、以後は書き換えない
    // One shippable unit; required values are taken at construction and never change afterwards
    public sealed class PlaytestOutboxBox
    {
        public readonly string Directory;
        public readonly string BundleId;
        public readonly PlaytestUploadKind Kind;

        public PlaytestOutboxBox(string directory, string bundleId, PlaytestUploadKind kind)
        {
            Directory = directory;
            BundleId = bundleId;
            Kind = kind;
        }
    }
}
