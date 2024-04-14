using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Block.Interface.Component;
using Game.Block.Interface.ComponentAttribute;

namespace Game.Block.Interface
{
    public interface IBlockComponentManager
    {
        //public T GetComponent<T>() where T : IBlockComponent;
        public T GetComponent<T>();

        //public bool ExistsComponent<T>() where T : IBlockComponent;
        public bool ExistsComponent<T>();

        //public bool TryGetComponent<T>(out T component) where T : IBlockComponent;
        public bool TryGetComponent<T>(out T component);
    }

    public class BlockComponentManager : IBlockComponentManager
    {
        private bool IsDestroy { get; set; }
        
        private readonly List<IBlockComponent> _blockComponents = new();
        private readonly Dictionary<Type,IBlockComponent> _disallowMultiple = new();

        //public T GetComponent<T>() where T : IBlockComponent
        public T GetComponent<T>()
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");

            return (T)_blockComponents.Find(x => x is T);
        }

        //public bool ExistsComponent<T>() where T : IBlockComponent
        public bool ExistsComponent<T>()
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");

            return _blockComponents.Exists(x => x is T);
        }

        //public bool TryGetComponent<T>(out T component) where T : IBlockComponent
        public bool TryGetComponent<T>(out T component)
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

        public void Destroy()
        {
            IsDestroy = true;
        }

        public void AddComponent(IBlockComponent blockComponent)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            CheckDisallowMultiple();

            _blockComponents.Add(blockComponent);

            #region Internal

            void CheckDisallowMultiple()
            {
                var disallowMultiple = Attribute.GetCustomAttribute(blockComponent.GetType(), typeof(DisallowMultiple));
                if (disallowMultiple == null) return;
                
                if (_disallowMultiple.ContainsKey(blockComponent.GetType()))
                {
                    throw new InvalidOperationException($"This component is already added. {blockComponent.GetType()}");
                }
                _disallowMultiple.Add(blockComponent.GetType(), blockComponent);
            }

          #endregion
        }
        
        public void AddComponents(IEnumerable<IBlockComponent> blockComponents)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");

            foreach (var blockComponent in _blockComponents)
            {
                AddComponent(blockComponent);
            }
        }

        public void RemoveComponent(IBlockComponent blockComponent)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");

            _blockComponents.Remove(blockComponent);
        }
    }
}