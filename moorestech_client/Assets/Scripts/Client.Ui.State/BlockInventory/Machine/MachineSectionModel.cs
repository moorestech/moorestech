using Client.Game.InGame.Block;
using Client.Ui.State.BlockDetail;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Recipe.Source;
using Game.Block.Interface.MachineUpgrade;
using Game.Block.Interface.State;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 追跡・枠・解放レシピ・上限の変化で配り直す
    /// Re-publishes on tracking, slots, recipes and limit changes
    /// </summary>
    public sealed class MachineSectionModel : IMachineSectionModel, IInitializable
    {
        private readonly OpenBlockDetailTracker _tracker;
        private readonly IBlockInventoryPanelSource _panelSource;
        private readonly UnlockedMachineRecipeSource _recipeSource;
        private readonly IModuleSlotStackLimitLookup _stackLimit;
        private readonly IFluidReplicaReader _fluidReplica;
        private readonly ReactiveProperty<SectionContent<MachineSectionViewModel>> _current = new(SectionContent<MachineSectionViewModel>.Absent);
        private MachineRecipeCatalog _catalog = MachineRecipeCatalog.Empty;

        public IReadOnlyReactiveProperty<SectionContent<MachineSectionViewModel>> Current => _current;

        public MachineSectionModel(OpenBlockDetailTracker tracker, IBlockInventoryPanelSource panelSource, UnlockedMachineRecipeSource recipeSource, IModuleSlotStackLimitLookup stackLimit, IFluidReplicaReader fluidReplica)
        {
            _tracker = tracker;
            _panelSource = panelSource;
            _recipeSource = recipeSource;
            _stackLimit = stackLimit;
            _fluidReplica = fluidReplica;
        }

        public void Initialize()
        {
            // レシピの要約と辞書はレシピ源が変わったときだけ組む（加工中の毎 tick の状態更新では組まない。D-39）
            // Build the recipe summaries and lookup only when the recipe source changes (never on per-tick state updates; D-39)
            _recipeSource.Current.Subscribe(recipes =>
            {
                _catalog = MachineRecipeCatalog.Build(recipes);
                Rebuild();
            });

            // パネルは alpha の Model が追跡替え・追跡の状態変化でも組み直すので、パネル・レシピ源・上限で組み直す
            // The panel is rebuilt by alpha's Model on tracking and state changes, so rebuild on panel, recipe source and limit
            _panelSource.Current.Subscribe(_ => Rebuild());
            _stackLimit.OnStackLimitChanged.Subscribe(_ => Rebuild());
        }

        private void Rebuild()
        {
            _current.Value = Read();

            #region Internal

            SectionContent<MachineSectionViewModel> Read()
            {
                // silent-ok: 閉・列車・取得失敗のパネルに機械の区画は無い（alpha の外殻が本文を作らない） / 並べ比べと録画
                if (_panelSource.Current.Value is not OpenBlockInventoryPanel panel || !panel.IsOpenWithoutError) return SectionContent<MachineSectionViewModel>.Absent;
                // silent-ok: 追跡ブロックが未解決の間は区画を出さない（閉と同じ見え方） / 追跡替えでパネルが組み直され直ちに再計算
                var block = _tracker.TrackedBlock;
                if (block == null) return SectionContent<MachineSectionViewModel>.Absent;
                // silent-ok: 追跡替え直後はパネルが前のブロックのまま届くことがある（パネルの ID と詳細を混ぜない） / パネルの組み直しで直ちに再計算
                if (!panel.BlockInstanceId.Equals(block.BlockInstanceId)) return SectionContent<MachineSectionViewModel>.Absent;
                if (!TryReadMachine(block, out var detail)) return SectionContent<MachineSectionViewModel>.Absent;
                var fluids = BlockFluidSlotReading.Read(block, _fluidReplica).Slots;
                return SectionContent<MachineSectionViewModel>.Present(MachineSectionViewModelBuilder.Build(panel, detail, panel.ItemSlots, fluids, _catalog, _stackLimit.CurrentStackLimit));
            }

            static bool TryReadMachine(BlockGameObject block, out MachineDetail detail)
            {
                detail = null;
                var common = block.GetStateDetail<CommonMachineBlockStateDetail>(CommonMachineBlockStateDetail.BlockStateDetailKey);
                var machine = block.GetStateDetail<MachineBlockStateDetail>(MachineBlockStateDetail.BlockStateDetailKey);
                // silent-ok: 2 つの状態詳細と機械の param が揃うブロックだけが機械（Web ホストと同じ判定） / 区画が出ないことを並べ比べで見る
                if (common == null || machine == null || block.BlockMasterElement.BlockParam is not IMachineParam param) return false;
                var gearDriven = GearDriveConversion.IsGearDriven(block, param);
                return MachineDetailConversion.TryBuild(common, machine, param, gearDriven, out detail);
            }

            #endregion
        }
    }
}
