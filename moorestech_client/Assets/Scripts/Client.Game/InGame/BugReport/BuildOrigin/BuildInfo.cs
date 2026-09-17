namespace Client.Game.InGame.BugReport.BuildOrigin
{
    // 配布ビルドの出所。plan E が StreamingAssets/build-info.json へ焼き、報告と進行記録が読むだけの契約（shared-contracts §1）
    // The distributed build's origin; plan E bakes StreamingAssets/build-info.json and reports/records only read it (shared-contracts §1)
    // 読むのは RepositoryStateProbe.ReadBuildOrigin() の1本だけ（ADR 0059）。二重パースを防ぐため新規リーダーを足さないこと
    // Only RepositoryStateProbe.ReadBuildOrigin() reads the file (ADR 0059); do not add another reader to avoid a duplicate parser
    // 焼かれていなかった値は実値と同じ形（""・false）で埋めずnullのまま運ぶ。false はクリーンな作業ツリーという実値に化ける（F02）
    // Values the bake lacked stay null instead of real-looking defaults ("" or false); false would pose as a real clean working tree (F02)
    public sealed class BuildInfo
    {
        public string Commit;
        public string Branch;
        public string MasterDataCommit;
        public bool? Dirty;

        // 焼く側（BuildInfoComposer）が出す masterDirty キー。shared-contracts §1には無いが、
        // 落とすとマスタの未コミット変更が常にクリーン扱いになるため保持する（ADR 0059）
        // The masterDirty key the baking side (BuildInfoComposer) emits. Absent from shared-contracts §1, but
        // dropping it would always report the master data as clean, so it is kept (ADR 0059)
        public bool? MasterDataDirty;

        public string SteamBuildLabel;
        public string BuiltAt;
        public string Target;
    }
}
