using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public interface IModalInstantiator
    {
        public UniTask<IModalObject> InstantiateModal();
    }
    
    public interface IModalObject
    {
        public void Initialize(int canvasSortOrder);
        
        UniTask<IModalResult> OpenModal();
    }
    
    public interface IModalResult { }
}