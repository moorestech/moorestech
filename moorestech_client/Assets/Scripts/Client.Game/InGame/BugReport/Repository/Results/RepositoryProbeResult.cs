using System.Collections.Generic;

namespace Client.Game.InGame.BugReport
{
    public sealed class RepositoryProbeResult
    {
        public RepositoryState State;
        public string DiffText = "";
        public List<string> UntrackedFiles = new();

        // HEADすら取れず状態を1つも名乗れないときの理由。これが入った結果は State が null
        // Why nothing at all could be read (not even HEAD); such a result carries a null State
        public string Error;

        // 取れた問い合わせは使いつつ、取れなかった問い合わせの理由を1つずつ残す
        // Keeps whatever could be read while recording, one by one, why the rest could not
        public List<string> QueryFailures = new();

        // 追跡ファイルに変更がある状態。未追跡だけのdirtyと区別しないと「差分が空」の異常を検出できない
        // Tracked files have changes; without separating this from untracked-only dirt, an empty diff cannot be flagged
        public bool TrackedChangesPresent;
    }
}
