using System;
using System.Collections.Generic;

namespace Client.Game.InGame.UI.UIState
{
    public interface IUIState
    {
        public void OnEnter(UITransitContext context);
        public UITransitContext GetNextUpdate();
        public void OnExit();
    }
    
    /// <summary>
    /// UI状態遷移時に渡されるコンテキスト情報
    /// Context information passed during UI state transition
    /// </summary>
    public class UITransitContext
    {
        public UIStateEnum LastStateEnum { get; private set; }
        private readonly UITransitContextContainer _container;

        public UITransitContext(UIStateEnum lastStateEnum)
        {
            LastStateEnum = lastStateEnum;
            _container = new UITransitContextContainer();
        }

        /// <summary>
        /// コンテキストに値を設定
        /// Set value to context
        /// </summary>
        public void SetContext<T>(T value)
        {
            _container.Set(value);
        }

        /// <summary>
        /// コンテキストから値を取得
        /// Get value from context
        /// </summary>
        public T GetContext<T>()
        {
            return _container.Get<T>();
        }
    }

    /// <summary>
    /// UIトランジットコンテキストのコンテナ
    /// Container for UI transit context
    /// </summary>
    internal class UITransitContextContainer
    {
        private readonly Dictionary<Type, object> _contexts = new Dictionary<Type, object>();

        /// <summary>
        /// コンテキストに値を設定
        /// Set value to context
        /// </summary>
        public void Set<T>(T value)
        {
            var type = typeof(T);
            _contexts[type] = value;
        }

        /// <summary>
        /// コンテキストから値を取得
        /// Get value from context
        /// </summary>
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