using System;
using UniRx;

namespace Client.Skit.Skit
{
    public interface ISkitActionContext
    {
        public bool IsAuto { get; }
        public bool IsSkip { get; }
        public IObservable<Unit> OnSkip { get; }
    }
}