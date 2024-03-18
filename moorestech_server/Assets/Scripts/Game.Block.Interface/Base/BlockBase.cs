using System;
using System.Collections.Generic;

namespace Game.Block.Interface.Base
{
    public abstract class BlockBase : IDisposable
    {
        public bool IsDestroy { get; private set; }

        public IReadOnlyList<BlockComponentBase> BlockComponents => _blockComponents;
        private readonly List<BlockComponentBase> _blockComponents = new();

        public void Dispose()
        {
            IsDestroy = true;
        }

        public void AddComponent(BlockComponentBase blockComponent)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            _blockComponents.Add(blockComponent);
        }
        
        public void RemoveComponent(BlockComponentBase blockComponent)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            _blockComponents.Remove(blockComponent);
        }
        
        public T GetComponent<T>() where T : BlockComponentBase
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            return (T) _blockComponents.Find(x => x is T);
        }
        
        public bool ExistsComponent<T>() where T : BlockComponentBase
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            return _blockComponents.Exists(x => x is T);
        }

        public bool TryGetComponent<T>(out T component) where T : BlockComponentBase
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");

            var result = _blockComponents.Find(x => x is T);
            if (result == null)
            {
                component = default;
                return false;
            }

            component = (T)result;
            return true;
        }
    }
}