// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
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