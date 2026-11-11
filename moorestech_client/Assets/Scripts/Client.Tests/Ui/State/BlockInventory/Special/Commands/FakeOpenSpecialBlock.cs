using Client.Ui.State.BlockInventory.Special.Commands;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    // 開いたブロックの偽物（null なら開いていない）
    // Fake open block; null means nothing is open
    public sealed class FakeOpenSpecialBlock : IOpenSpecialBlock
    {
        private OpenSpecialBlock? _block;

        public void SetOpen(OpenSpecialBlock? block)
        {
            _block = block;
        }

        public bool TryGet(out OpenSpecialBlock block)
        {
            block = _block.GetValueOrDefault();
            return _block.HasValue;
        }
    }
}
