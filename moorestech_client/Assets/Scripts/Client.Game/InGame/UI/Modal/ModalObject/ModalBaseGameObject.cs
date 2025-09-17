using Client.Common.Asset;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public abstract class ModalBaseInstantiator : IModalInstantiator
    {
        private readonly string _addressablePath;
        
        protected ModalBaseInstantiator(string addressablePath)
        {
            _addressablePath = addressablePath;
        }
        
        public async UniTask<IModalObject> InstantiateModal()
        {
            var prefab = await AddressableLoader.LoadAsync<GameObject>(_addressablePath);
            var instance = Object.Instantiate(prefab.Asset);
            prefab.Dispose();
            
            return instance.GetComponent<IModalObject>();
        }
    }
    
    public abstract class ModalBaseGameObject : MonoBehaviour, IModalObject
    {
        [SerializeField] private Canvas canvas;
        
        public void Initialize(int canvasSortOrder)
        {
            canvas.sortingOrder = canvasSortOrder;
        }
        
        public abstract UniTask<IModalResult> OpenModal();
    }
}