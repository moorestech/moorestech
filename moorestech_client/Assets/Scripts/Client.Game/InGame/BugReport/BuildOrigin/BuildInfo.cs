namespace Client.Game.InGame.BugReport.BuildOrigin
{
    // 配布ビルドの出所。plan E が StreamingAssets/build-info.json へ焼き、報告と進行記録が読むだけの契約（shared-contracts §1）
    // The distributed build's origin; plan E bakes StreamingAssets/build-info.json and reports/records only read it (shared-contracts §1)
    // 読むのは RepositoryStateProbe.ReadBuildInfo() の1本だけ（ADR 0059）。二重パースを防ぐため新規リーダーを足さないこと
    // Only RepositoryStateProbe.ReadBuildInfo() reads the file (ADR 0059); do not add another reader to avoid a duplicate parser
    public sealed class BuildInfo
    {
        public string Commit;
        public string Branch;
        public string MasterDataCommit;
        public bool Dirty;

        // 焼く側（ComposeBuildInfoJson）が既に出している実キー。shared-contracts §1には無いが、
        // 落とすとマスタの未コミット変更が常にクリーン扱いになるため保持する（ADR 0059の帰結・本タスクでの追加）
        // The baking side (ComposeBuildInfoJson) already emits this key. Absent from shared-contracts §1, but
        // dropping it would always report the master data as clean; kept for fidelity (ADR 0059 follow-on, added by this task)
        public bool MasterDataDirty;

        // 焼く側が未対応（plan E待ち）のため現状は常にnull
        // Not yet baked by the writer side (pending plan E), so these stay null for now
        public string SteamBuildLabel;
        public string BuiltAt;
        public string Target;
    }
}
