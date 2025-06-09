using UniRx;
using UnityEngine;

namespace Client.Game.Skit.Starter
{
    public class PlayerSkitStarterDetector : MonoBehaviour
    {
        public Subject<bool> OnStateChange => _onStateChange;
        private readonly Subject<bool> _onStateChange = new();
        public bool IsStartReady => CurrentSkitStarterObject != null;
        public SkitStarterObject CurrentSkitStarterObject { get; private set; }
        
        private void OnDisable()
        {
            _onStateChange.OnNext(false);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<SkitStarterObject>(out var storyStarterObject))
            {
                CurrentSkitStarterObject = storyStarterObject;
                _onStateChange.OnNext(true);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<SkitStarterObject>(out var _))
            {
                CurrentSkitStarterObject = null;
            }
        }
    }
}