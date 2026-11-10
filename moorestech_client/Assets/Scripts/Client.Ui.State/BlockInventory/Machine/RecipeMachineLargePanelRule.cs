using System;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Panel;
using Game.Block.Interface;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// レシピを選べる機械だけ大型パネルにする
    /// Makes only machines with selectable recipes large, as Web BlockInventoryPanel isLargeMachinePanel
    /// </summary>
    public sealed class RecipeMachineLargePanelRule : IBlockInventoryLargePanelRule, IInitializable
    {
        private readonly IMachineSectionModel _model;
        private readonly Subject<Unit> _ruleInputChanged = new();
        private bool _lastPresent;
        private BlockInstanceId _lastBlockInstanceId;
        private bool _lastHasRows;

        public IObservable<Unit> OnRuleInputChanged => _ruleInputChanged;

        public RecipeMachineLargePanelRule(IMachineSectionModel model)
        {
            _model = model;
        }

        public void Initialize()
        {
            _model.Current.Subscribe(OnMachineChanged);

            #region Internal

            void OnMachineChanged(SectionContent<MachineSectionViewModel> current)
            {
                // 判定の入力（有無・ブロック・行の有無）が変わったときだけ流す。加工中の毎 tick の状態更新では流さない（D-39）
                // Emit only when the rule's inputs (presence, block, any rows) change; never on per-tick processing updates (D-39)
                var present = current.IsPresent;
                var blockInstanceId = present ? current.Value.BlockInstanceId : default;
                var hasRows = present && 0 < current.Value.Rows.Count;
                if (present == _lastPresent && blockInstanceId.Equals(_lastBlockInstanceId) && hasRows == _lastHasRows) return;
                _lastPresent = present;
                _lastBlockInstanceId = blockInstanceId;
                _lastHasRows = hasRows;
                _ruleInputChanged.OnNext(Unit.Default);
            }

            #endregion
        }

        public bool IsLargePanel(OpenBlockInventoryPanel panel)
        {
            var current = _model.Current.Value;
            return current.IsPresent && current.Value.IsFor(panel) && 0 < current.Value.Rows.Count;
        }
    }
}
