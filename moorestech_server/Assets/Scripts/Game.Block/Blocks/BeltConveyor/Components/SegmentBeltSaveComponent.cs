using Game.Block.Interface.Component;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class SegmentBeltSaveComponent : IBlockSaveState
    {
        internal static string SaveKeyStatic => typeof(SegmentBeltSaveComponent).FullName;
        public string SaveKey => SaveKeyStatic;
        public bool IsDestroy { get; private set; }
        private readonly SegmentBeltComponent _belt;
        private readonly IBeltWorldMutation _world;
        internal SegmentBeltSaveComponent(SegmentBeltComponent belt, IBeltWorldMutation world) { _belt = belt; _world = world; }
        public object GetSaveState() => _world.CaptureCell(_belt);
        public void Destroy() => IsDestroy = true;
    }
}
