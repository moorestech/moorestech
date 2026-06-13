using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // クリーンルームのドアハッチ（Task 5 で本実装に置き換える空実装スタブ）
    // Clean-room door hatch (empty stub; replaced by the real implementation in Task 5)
    public class CleanRoomDoorHatchComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
