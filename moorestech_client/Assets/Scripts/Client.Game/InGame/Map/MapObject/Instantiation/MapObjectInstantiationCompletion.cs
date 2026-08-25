using System;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     生成の成功だけを完了フラグへ反映する
    ///     Reflects only successful instantiation into the completion flag
    /// </summary>
    internal sealed class MapObjectInstantiationCompletion
    {
        private readonly ReactiveProperty<bool> _isCompletedSuccessfully = new(false);
        private readonly UniTaskCompletionSource _completion = new();

        public IReadOnlyReactiveProperty<bool> GetSuccessfulCompletionState()
        {
            return _isCompletedSuccessfully;
        }

        public UniTask WaitAsync()
        {
            return _completion.Task;
        }

        public void Complete()
        {
            _isCompletedSuccessfully.Value = true;
            _completion.TrySetResult();
        }

        public void Fail(Exception exception)
        {
            _completion.TrySetException(exception);
        }

        public void Cancel()
        {
            _completion.TrySetCanceled();
        }
    }
}
