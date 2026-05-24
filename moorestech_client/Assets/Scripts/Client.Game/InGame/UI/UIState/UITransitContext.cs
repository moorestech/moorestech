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
        
        public bool TryGetContext<T>(out T context)
        {
            context = GetContext<T>();
            return !EqualityComparer<T>.Default.Equals(context, default);
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
        
        public UITransitContextContainer() { }
        
        public static UITransitContextContainer Create<T>(T value)
        {
            var container = new UITransitContextContainer();
            container.Set(value);
            return container;
        }
        
        public static UITransitContextContainer Create<T1, T2>(T1 value1, T2 value2)
        {
            var container = new UITransitContextContainer();
            container.Set(value1);
            container.Set(value2);
            return container;
        }
        
        public static UITransitContextContainer Create<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
        {
            var container = new UITransitContextContainer();
            container.Set(value1);
            container.Set(value2);
            container.Set(value3);
            return container;
        }
    }
}