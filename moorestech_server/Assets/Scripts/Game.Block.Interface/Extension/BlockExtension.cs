using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Interface.Extension
{
    public static class BlockExtension
    {
        public static T GetComponent<T>(this IBlock block) where T : IBlockComponent
        {
            return block.ComponentManager.GetComponent<T>();
        }
        
        public static List<T> GetComponents<T>(this IBlock block) where T : IBlockComponent
        {
            return block.ComponentManager.GetComponents<T>();
        }
        
        public static bool ExistsComponent<T>(this IBlock block) where T : IBlockComponent
        {
            return block.ComponentManager.ExistsComponent<T>();
        }
        
        public static bool TryGetComponent<T>(this IBlock block, out T component) where T : IBlockComponent
        {
            return block.ComponentManager.TryGetComponent(out component);
        }
    }
}