namespace Client.Starter.PlaytestSmoke
{
    /// <summary>
    /// 通し検証の段階。引数の文字列は StandalonePlaytestSmokeSettings.TryParse で1回だけこれへ変換する
    /// The smoke run's phase; the argument string is converted to this exactly once in StandalonePlaytestSmokeSettings.TryParse
    /// </summary>
    public enum StandalonePlaytestSmokePhase
    {
        // 新規ワールド/チュートリアル開始/セーブ
        // Fresh world, tutorial start and save
        PhaseOne,

        // phase1のセーブを読み、報告送信/アップロード確認
        // Loads phase1's save, then sends a report and confirms the upload
        PhaseTwo,
    }
}
