using System;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 研究ツリーの選択を持つ。画面を離れたら捨てる（Web はパネルのアンマウントで選択が消える）
    /// Holds the research tree selection and drops it on leaving the screen (the Web loses it when the panel unmounts)
    /// </summary>
    public sealed class ResearchSelectionStore : IInitializable
    {
        private readonly UiRoutingState _routing;
        private readonly ReactiveProperty<ResearchSelection> _current = new(ResearchSelection.None);

        public IReadOnlyReactiveProperty<ResearchSelection> Current => _current;

        public ResearchSelectionStore(UiRoutingState routing)
        {
            _routing = routing;
        }

        public void Initialize()
        {
            _routing.Current.Subscribe(snapshot =>
            {
                // silent-ok: 研究画面の中の経路の通知は選択を保つ / 離れたときの None は録画 uitk-research-tree が見る
                if (snapshot.Screen == UiScreen.ResearchTree) return;
                _current.Value = ResearchSelection.None;
            });
        }

        public void Toggle(Guid researchGuid)
        {
            _current.Value = _current.Value.Toggled(researchGuid);
        }

        public void Clear()
        {
            _current.Value = ResearchSelection.None;
        }
    }
}
