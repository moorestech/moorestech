using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public interface IModalInstantiator
    {
        UniTask<IModalObject> InstantiateModal();
    }
    
    public interface IModalObject
    {
        void Initialize(int canvasSortOrder);
        
        UniTask<IModalResult> OpenModal(CancellationToken token);
        
        void DestroyModal();
    }
    
    public interface IModalResult { }
}