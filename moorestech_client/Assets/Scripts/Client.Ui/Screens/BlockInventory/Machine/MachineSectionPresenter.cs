using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.BlockInventory.Panel;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の Presenter
    /// Machine section presenter: feeds machine and context models into the view; handles only mode-changing presses
    /// </summary>
    public sealed class MachineSectionPresenter : IBlockInventorySection
    {
        private readonly MachineSectionView _view;
        private readonly IMachineSectionModel _machine;
        private readonly IBlockContextModel _context;
        private readonly MachineSelectionModeState _mode = new();
        private readonly ReactiveProperty<bool> _isApplicable = new(false);
        private readonly CompositeDisposable _subscriptions = new();

        public VisualElement Root => _view.Root;
        public IReadOnlyReactiveProperty<bool> IsApplicable => _isApplicable;

        public MachineSectionPresenter(MachineSectionView view, IMachineSectionModel machine, IBlockContextModel context)
        {
            _view = view;
            _machine = machine;
            _context = context;
        }

        public void Attach(OpenBlockInventoryPanel panel)
        {
            // 機械の通知は本体を、文脈の通知は文脈の 3 欄だけを描く（1 tick に本体を 2 回描かない。D-39）
            // Machine updates draw the body and context updates draw only the context entries (no double body render per tick; D-39)
            _machine.Current.Subscribe(_ => Apply()).AddTo(_subscriptions);
            _context.Current.Subscribe(_view.RenderContext).AddTo(_subscriptions);
            _view.OnChangeRecipeRequested.Subscribe(_ =>
            {
                _mode.OpenSelection();
                Apply();
            }).AddTo(_subscriptions);

            // 行の押下は要求を覚えて描き直すだけ（送る判断はコマンド、送信とトーストは View。D-24・D-33）
            // A row press only remembers the request and redraws (the command decides sending; the view sends and toasts; D-24, D-33)
            _view.OnRecipeRowPressed.Subscribe(recipeGuid =>
            {
                _mode.RememberRequest(recipeGuid);
                Apply();
            }).AddTo(_subscriptions);
        }

        public void Detach()
        {
            // 外れたら偽へ戻す（前のパネルの適用可否を次のパネルへ持ち越さない）
            // Reset to false when detached so the previous panel's applicability is not carried over
            _subscriptions.Clear();
            _isApplicable.Value = false;
        }

        private void Apply()
        {
            // 描いてから出す
            // Draw before showing; hide without drawing when not a machine
            var machine = _machine.Current.Value;
            if (machine.IsPresent) _view.Render(machine.Value, _mode.ShowsSelection(machine.Value));
            _isApplicable.Value = machine.IsPresent;
        }
    }
}
