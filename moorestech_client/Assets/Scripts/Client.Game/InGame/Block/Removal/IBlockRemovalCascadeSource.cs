using System.Collections.Generic;
using Game.Train.SaveLoad;

namespace Client.Game.InGame.Block.Removal
{
    // 自ブロックから消える接続の端点を列挙する
    // Enumerate connection endpoints removed with this block
    public interface IBlockRemovalCascadeSource
    {
        void CollectConnectionDestinations(List<ConnectionDestination> destinations);
    }
}
