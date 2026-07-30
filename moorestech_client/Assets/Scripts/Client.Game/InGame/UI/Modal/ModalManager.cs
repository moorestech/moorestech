// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
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