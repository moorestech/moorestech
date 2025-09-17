using System;
using System.Threading;
using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public abstract class ModalInstantiatorBase : IModalInstantiator
    {
        private readonly string _addressablePath;
        
        protected ModalInstantiatorBase(string addressablePath)
        {
            _addressablePath = addressablePath;
        }
        
        public virtual async UniTask<IModalObject> InstantiateModal()
        {
            var prefab = await AddressableLoader.LoadAsync<GameObject>(_addressablePath);
            var instance = Object.Instantiate(prefab.Asset);
            prefab.Dispose();
            
            return instance.GetComponent<IModalObject>();
        }
    }
    
    public abstract class ModalGameObjectBase : MonoBehaviour, IModalObject
    {
        protected IObservable<Unit> OnCloseButtonClick => _closeButtonClickSubject.AsObservable();
        private readonly Subject<Unit> _closeButtonClickSubject = new();
        
        [SerializeField] private Canvas canvas;
        
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backgroundButton;
        
        private void Awake()
        {
            closeButton.onClick.AddListener(() => _closeButtonClickSubject.OnNext(Unit.Default));
            backgroundButton.onClick.AddListener(() => _closeButtonClickSubject.OnNext(Unit.Default));
        }
        
        
        public void Initialize(int canvasSortOrder)
        {
            canvas.sortingOrder = canvasSortOrder;
        }
        public abstract UniTask<IModalResult> OpenModal(CancellationToken token);
        
        
        public void DestroyModal()
        {
            Destroy(gameObject);
        }
    }
}