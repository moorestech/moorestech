using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Core.Item.Interface;
using UnityEngine;

namespace GameState
{
    public interface IBlockRegistry
    {
        IReadOnlyBlock GetBlock(Vector3Int position);
        IReadOnlyDictionary<Vector3Int, IReadOnlyBlock> AllBlocks { get; }
    }

    public interface IReadOnlyBlock
    {
        int BlockId { get; }
        Vector3Int Position { get; }
        BlockDirection Direction { get; }
        T GetState<T>(string stateKey) where T : class;
        UniTask<IBlockInventory> GetInventoryAsync();
    }

    public interface IBlockInventory
    {
        IReadOnlyList<IItemStack> Items { get; }
        System.DateTime LastUpdated { get; }
    }

    public enum BlockDirection
    {
        North,
        East,
        South,
        West
    }
}