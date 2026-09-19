namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 宣言に載せる順。件数・総量の上限は先頭から埋まるので、箱の本体を先に、静止画を最後に置く
    // The order files enter the declaration; the count/total caps fill from the front, so the box's core goes first and the stills last
    public enum PlaytestBundleFileRank
    {
        // 欠けると箱として意味を成さない。見送れず、失敗は箱の失敗として数える
        // Without it the box means nothing; never skipped, and its failure counts against the box
        Required = 0,
        Supporting = 1,
        Frames = 2,
    }

    // 箱の中のパスの格付け。箱の中身の語彙は箱の種類ごとの実装だけが知り、アップローダーは格付けしか見ない
    // Ranks a path inside a box; only the per-kind implementations know a box's layout, and the uploader sees nothing but the rank
    public interface IPlaytestBoxFilePolicy
    {
        PlaytestBundleFileRank RankOf(string relativePath);
    }
}
