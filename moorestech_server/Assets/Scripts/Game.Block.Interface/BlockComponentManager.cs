using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.Component;
using Game.Block.Interface.ComponentAttribute;

namespace Game.Block.Interface
{
    public interface IBlockComponentManager
    {
        public T GetComponent<T>() where T : IBlockComponent;
        
        public bool ExistsComponent<T>() where T : IBlockComponent;
        
        public bool TryGetComponent<T>(out T component) where T : IBlockComponent;
    }
    
    public class BlockComponentManager : IBlockComponentManager
    {
        private readonly List<IBlockComponent> _blockComponents = new();
        private readonly Dictionary<Type, IBlockComponent> _disallowMultiple = new();
        private bool IsDestroy { get; set; }
        
        public T GetComponent<T>() where T : IBlockComponent
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            return (T)_blockComponents.Find(x => x is T);
        }
        
        public bool ExistsComponent<T>() where T : IBlockComponent
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            return _blockComponents.Exists(x => x is T);
        }
        
        public bool TryGetComponent<T>(out T component) where T : IBlockComponent
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
            foreach (var blockComponent in _blockComponents) blockComponent.Destroy();
        }
        
        public void AddComponent(IBlockComponent blockComponent)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            CheckDisallowMultiple();
            
            _blockComponents.Add(blockComponent);
            
            #region Internal
            
            void CheckDisallowMultiple()
            {
                var componentType = blockComponent.GetType();
                var interfaces = componentType.GetInterfaces();
                
                foreach (var iface in interfaces)
                {
                    var attrs = iface.GetCustomAttributes(typeof(DisallowMultiple), true);
                    if (attrs.Length == 0) continue;
                    
                    if (_blockComponents.Any(c => iface.IsInstanceOfType(c)))
                    {
                        throw new InvalidOperationException($"{iface.Name}は既に追加されています。");
                    }
                }
            }
            
            #endregion
        }
        
        public void AddComponents(IEnumerable<IBlockComponent> blockComponents)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            foreach (var blockComponent in blockComponents) AddComponent(blockComponent);
        }
        
        public void RemoveComponent(IBlockComponent blockComponent)
        {
            if (IsDestroy) throw new InvalidOperationException("Block is already destroyed");
            
            _blockComponents.Remove(blockComponent);
        }
    }
}