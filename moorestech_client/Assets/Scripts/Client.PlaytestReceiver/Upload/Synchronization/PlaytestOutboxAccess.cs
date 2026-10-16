using System.IO;
using System.Threading;

namespace Client.PlaytestReceiver.Upload
{
    // 補完中の箱を配送が宣言しないよう、全走行と起動時補完を直列化する
    // Serialize startup completion and upload runs so a box is never declared mid-completion
    public static class PlaytestOutboxAccess
    {
        public static readonly SemaphoreSlim Gate = new(1, 1);

        // 同じ箱の補完と宣言は別Editor間でも排他する。ロックファイル自体は消さない
        // Serialize completion and declaration across Editors; never unlink the lock file itself
        public static FileStream AcquireBox(string directory)
        {
            var stream = new FileStream(Path.Combine(directory, PlaytestOutboxScanner.LockMarker), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            // 外部ディスクIO境界。UnixのEditor間でもOSの範囲ロックを取り、失敗は呼出側の保留記録へ戻す
            // External disk IO boundary: take an OS range lock across Unix Editors; callers record failures as deferred work
            try { stream.Lock(0, 1); return stream; }
            catch (IOException error)
            {
                stream.Dispose();
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] box lock unavailable: {directory}: {error.Message}");
                throw;
            }
        }
    }
}
