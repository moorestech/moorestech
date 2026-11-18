using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Items;
using Core.Master;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.State.Inventory.Grab
{
    /// <summary>
    /// 今掴んでいるアイテム。掴みの流れのたびに読み直し、変わったときだけ流す
    /// The currently held item; re-read on each grab notification and published only when it changes
    /// </summary>
    public sealed class PlayerGrabModel : IInitializable
    {
        private readonly IPlayerInventoryOperations _operations;
        private readonly ReactiveProperty<SlotItem> _current = new(new SlotItem(ItemMaster.EmptyItemId, 0));

        public IReadOnlyReactiveProperty<SlotItem> Current => _current;

        public PlayerGrabModel(IPlayerInventoryOperations operations)
        {
            _operations = operations;
        }

        public void Initialize()
        {
            _current.Value = _operations.ReadGrab();
            _operations.OnGrabChanged.Subscribe(_ => _current.Value = _operations.ReadGrab());
        }
    }
}
