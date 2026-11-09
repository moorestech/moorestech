using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Equipment;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Panel
{
    /// <summary>
    /// 持ち物と装備の変化を購読し、描画用の値を同期で配る（Web InventoryTopic の写し。フレーム末への間引きはしない A-57）
    /// Subscribes to inventory and equipment changes and publishes draw values synchronously (Web InventoryTopic; no end-of-frame coalescing, A-57)
    /// </summary>
    public sealed class PlayerInventoryPanelModel : IInitializable
    {
        private readonly IPlayerInventoryOperations _operations;
        private readonly IEquipmentSelectionPort _equipment;
        private readonly ReactiveProperty<PlayerInventoryViewModel> _current;

        public IReadOnlyReactiveProperty<PlayerInventoryViewModel> Current => _current;

        public PlayerInventoryPanelModel(IPlayerInventoryOperations operations, IEquipmentSelectionPort equipment)
        {
            _operations = operations;
            _equipment = equipment;

            // 初期値を ctor で組み、Presenter の初期化の順に依らず null を配らない（前例 ModeHudModel）
            // Seed in the ctor so null is never published regardless of presenter initialization order (precedent ModeHudModel)
            _current = new ReactiveProperty<PlayerInventoryViewModel>(Build());
        }

        public void Initialize()
        {
            // 合図を購読し、購読までの間の変化も取り込む
            // Subscribe to the signal and pick up any change made before subscribing
            _operations.OnSnapshotChanged.Subscribe(_ => _current.Value = Build());
            _current.Value = Build();
        }

        private PlayerInventoryViewModel Build()
        {
            // 掴みは C4-e の掴み絵が持つので、main と装備だけを読む
            // The grab belongs to the C4-e grab picture, so only main and equipment are read
            var snapshot = _operations.ReadSnapshot();
            var itemGuids = InventoryItemGuidTable.Build(snapshot.MainSlots);
            return new PlayerInventoryViewModel(snapshot.MainSlots, snapshot.Equipment, _equipment.SelectedIndex, itemGuids);
        }
    }
}
