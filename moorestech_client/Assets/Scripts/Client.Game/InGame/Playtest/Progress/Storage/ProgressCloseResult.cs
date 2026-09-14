namespace Client.Game.InGame.Playtest.Progress
{
    // 閉じられなかった理由を「畳む中身が無い」と「書けずに失敗した」で分ける。どちらも null で返すと終了コードが成功へ潰れる
    // Separates "nothing to fold" from "the write failed": returning null for both collapses the shutdown result into success
    internal readonly struct ProgressCloseResult
    {
        // 畳めた outbox の箱。閉じられなければ null
        // The outbox box it folded into; null when nothing was closed
        public readonly string BundleDirectory;

        // 書けずに閉じられなかった（＝中身が無かったのではない）
        // Could not close because the write failed, as opposed to having nothing to write
        public readonly bool WriteFailed;

        private ProgressCloseResult(string bundleDirectory, bool writeFailed)
        {
            BundleDirectory = bundleDirectory;
            WriteFailed = writeFailed;
        }

        public static ProgressCloseResult Closed(string bundleDirectory)
        {
            return new ProgressCloseResult(bundleDirectory, false);
        }

        public static ProgressCloseResult NothingToClose()
        {
            return new ProgressCloseResult(null, false);
        }

        public static ProgressCloseResult Failed()
        {
            return new ProgressCloseResult(null, true);
        }
    }
}
