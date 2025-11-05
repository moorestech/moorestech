using System;
using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState
{
    /// <summary>
    /// UI状態遷移時に渡されるコンテキスト情報
    /// Context information passed during UI state transition
    /// </summary>
    public class UITransitContext
    {
        public UIStateEnum NextStateEnum { get; private set; }
        public UIStateEnum LastStateEnum { get; private set; }
        private readonly UITransitContextContainer _container;
        
        public UITransitContext(UIStateEnum nextStateEnum, UITransitContextContainer container = null)
        {
            NextStateEnum = nextStateEnum;
            _container = container;
        }
        
        public void SetLastState(UIStateEnum lastStateEnum)
        {
            LastStateEnum = lastStateEnum;
        }
        
        public T GetContext<T>()
        {
            return _container == null ? default : _container.Get<T>();
        }
    }
    
    /// <summary>
    /// UIトランジットコンテキストのコンテナ
    /// Container for UI transit context
    /// </summary>
    public class UITransitContextContainer
    {
        private readonly Dictionary<Type, object> _contexts = new();
        
        public void Set<T>(T value)
        {
            var type = typeof(T);
            _contexts[type] = value;
        }
        
        public T Get<T>()
        {
            var type = typeof(T);
            if (_contexts.TryGetValue(type, out var value))
            {
                return (T)value;
            }
            
            return default;
        }
    }
}