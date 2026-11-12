namespace Client.Ui.State.Challenge.Tree
{
    /// <summary>
    /// 解放済みのカテゴリが無いチャレンジ一覧（題だけ。Web は selected が無いと TreeView を描かない）
    /// Challenge list with no unlocked category (title only; the Web skips TreeView without a selection)
    /// </summary>
    public sealed class EmptyChallengeListViewModel : ChallengeListViewModel
    {
        internal EmptyChallengeListViewModel()
        {
        }
    }
}
