using System.Threading;
using Client.Game.InGame.UI.Modal.ModalObject;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.Modal
{
    public class ModalManager
    {
        private int _modalIndex = 0;
        
        public async UniTask<IModalResult> OpenModal(IModalInstantiator modalInstantiator, CancellationToken token)
        {
            var modalObject = await modalInstantiator.InstantiateModal();
            
            modalObject.Initialize(_modalIndex);
            
            _modalIndex++;
            var result = await modalObject.OpenModal(token);
            _modalIndex--;
            
            modalObject.DestroyModal();
            
            return result;
        }
    }
}