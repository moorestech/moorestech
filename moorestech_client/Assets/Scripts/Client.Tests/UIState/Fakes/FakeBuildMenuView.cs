using Client.Game.InGame.UI.BuildMenu;

namespace Client.Tests.UIState.Fakes
{
    /// <summary>
    ///     ビルドメニューの表示状態だけを記録し、エントリ選択は常に空を返すテスト用の代替実装
    ///     Test double that records only the menu's active state and never yields a selected entry
    /// </summary>
    public class FakeBuildMenuView : IBuildMenuView
    {
        public bool IsActive { get; private set; }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        public bool TryConsumeSelectedEntry(out BuildMenuEntry entry)
        {
            entry = default;
            return false;
        }
    }
}
