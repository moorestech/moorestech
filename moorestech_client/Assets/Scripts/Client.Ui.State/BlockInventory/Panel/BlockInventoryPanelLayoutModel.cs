using System.Collections.Generic;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Panel
{
    /// <summary>
    /// パネルの状態と寄与から大きさを決め、状態と大きさの組を配る。列車は常に大、ブロックは寄与のどれかが真なら大で本文の高さを確定する
    /// Decides the panel size from the state and contributions and publishes the (state, size) pair; trains are always large, blocks are large with a fixed body height when any rule says so
    /// </summary>
    public sealed class BlockInventoryPanelLayoutModel : IInitializable
    {
        private readonly IBlockInventoryPanelSource _panel;
        private readonly IReadOnlyList<IBlockInventoryLargePanelRule> _rules;
        private readonly ReactiveProperty<BlockInventoryPanelPresentation> _current = new(new BlockInventoryPanelPresentation(BlockInventoryPanelState.Closed, BlockInventoryPanelLayout.Natural));

        public IReadOnlyReactiveProperty<BlockInventoryPanelPresentation> Current => _current;   // 型を狭める公開（前例 SettingsModel.Current）

        public BlockInventoryPanelLayoutModel(IBlockInventoryPanelSource panel, IReadOnlyList<IBlockInventoryLargePanelRule> rules)
        {
            _panel = panel;
            _rules = rules;
        }

        public void Initialize()
        {
            _panel.Current.Subscribe(_ => Recompute());
            foreach (var rule in _rules) rule.OnRuleInputChanged.Subscribe(_ => Recompute());
            Recompute();

            #region Internal

            void Recompute()
            {
                // 状態と大きさを 1 回の再計算で組にして流す。画面は派生元（パネルの状態）を別に購読しない（D-55）
                // Publish state and size as one pair per recompute; the screen does not subscribe to the source panel state separately (D-55)
                var state = _panel.Current.Value;
                var layout = state switch
                {
                    OpenTrainInventoryPanel => BlockInventoryPanelLayout.LargeNaturalBody,
                    // 取得失敗のブロックは小型（Web は失敗の DTO に機械が載らず小型）。寄与はエラー無しのブロックにだけ聞く（D-60）
                    // A failed block stays natural (the Web failure DTO carries no machine); rules are asked only for error-free blocks (D-60)
                    OpenBlockInventoryPanel block when block.Error != BlockInventoryPanelError.None => BlockInventoryPanelLayout.Natural,
                    OpenBlockInventoryPanel block => AnyLarge(block) ? BlockInventoryPanelLayout.LargeFilledBody : BlockInventoryPanelLayout.Natural,
                    _ => BlockInventoryPanelLayout.Natural,
                };
                _current.Value = new BlockInventoryPanelPresentation(state, layout);
            }

            bool AnyLarge(OpenBlockInventoryPanel block)
            {
                foreach (var rule in _rules)
                {
                    if (rule.IsLargePanel(block)) return true;
                }
                return false;
            }

            #endregion
        }
    }
}
