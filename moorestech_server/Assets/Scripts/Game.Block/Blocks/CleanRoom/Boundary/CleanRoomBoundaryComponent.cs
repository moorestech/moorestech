using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 境界ブロック共通のマーカー。種別のみ保持し、可変状態は持たない。
    // Shared marker for boundary blocks; holds kind only, no mutable state.
    public class CleanRoomBoundaryComponent : ICleanRoomBoundaryComponent
    {
        public CleanRoomBoundaryKind BoundaryKind { get; }
        public bool IsDestroy { get; private set; }

        public CleanRoomBoundaryComponent(CleanRoomBoundaryKind kind)
        {
            BoundaryKind = kind;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
