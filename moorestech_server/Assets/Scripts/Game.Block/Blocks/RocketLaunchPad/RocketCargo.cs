using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;

namespace Game.Block.Blocks.RocketLaunchPad
{
    // ロケットに積んだ積荷と資材。打ち上げで消費され、打ち上げ前の撤去では返る
    // Payload and materials loaded into a rocket; consumed by the launch and refunded on removal before it
    public class RocketCargo
    {
        internal readonly IItemStack Payload;
        internal readonly IReadOnlyList<IItemStack> Materials;
        public bool IsLoaded => 0 < Payload.Count;

        public RocketCargo(IItemStack payload, IReadOnlyList<IItemStack> materials)
        {
            Payload = payload;
            Materials = materials;
        }

        // 欠損マスタの刈り込みで空になった資材は返さない
        // Materials emptied by missing-master pruning are not refunded
        public IEnumerable<IItemStack> AllItems()
        {
            return Materials.Prepend(Payload).Where(item => 0 < item.Count);
        }
    }
}
