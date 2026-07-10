using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    /// <summary>
    ///     境界種別を保持するだけのマーカーコンポーネント
    ///     Marker component that only holds its boundary kind
    /// </summary>
    public class CleanRoomBoundaryComponent : ICleanRoomBoundaryComponent
    {
        public CleanRoomBoundaryKind BoundaryKind { get; }
        public bool IsDestroy { get; private set; }

        public CleanRoomBoundaryComponent(CleanRoomBoundaryKind boundaryKind)
        {
            BoundaryKind = boundaryKind;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
