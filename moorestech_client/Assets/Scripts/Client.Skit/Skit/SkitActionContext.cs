using System;
using UniRx;
using UnityEngine;

namespace Client.Skit.Skit
{
    public interface ISkitActionController : ISkitActionContext
    {
        public void SetAuto(bool isAuto);
        
        public void SetSkip(bool isSkip);
    }
    
    public class SkitActionContext : ISkitActionController
    {
        public bool IsAuto { get; private set; }
        public bool IsSkip { get; private set; }
        
        public IObservable<Unit> OnSkip => _onSkip;
        private readonly Subject<Unit> _onSkip = new();
        
        public void SetAuto(bool isAuto)
        {
            IsAuto = isAuto;
        }
        public void SetSkip(bool isSkip)
        {
            IsSkip = isSkip;
            if (isSkip)
            {
                _onSkip.OnNext(Unit.Default);
            }
        }
    }
}