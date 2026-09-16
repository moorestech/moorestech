using System.IO;
using Client.PlaytestReceiver.Upload;

namespace Client.Tests.PlaytestReceiver
{
    // READY付きの箱を中身ごと作る。アップロード系テストの共通の下ごしらえ
    // Builds a READY-marked box with its payload; the shared setup of the upload tests
    internal static class PlaytestOutboxTestBoxes
    {
        public static string Make(string outbox, string bundleId, params (string Name, string Content)[] files)
        {
            var box = Path.Combine(outbox, bundleId);
            Directory.CreateDirectory(box);
            foreach (var file in files) File.WriteAllText(Path.Combine(box, file.Name), file.Content);
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.ReadyMarker), "");
            return box;
        }
    }
}
