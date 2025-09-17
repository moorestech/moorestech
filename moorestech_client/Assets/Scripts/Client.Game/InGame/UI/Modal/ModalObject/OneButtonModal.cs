using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public class OneButtonModal : ModalGameObjectBase
    {
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonText;
        
        public void OneModalInitialize(string modalText)
        {
            confirmButtonText.text = modalText;
        }
        
        public override async UniTask<IModalResult> OpenModal(CancellationToken token)
        {
            var closeTask = OnCloseButtonClick.ToUniTask(cancellationToken: token);
            var confirmTask = confirmButton.OnClickAsObservable().ToUniTask(cancellationToken: token);
            
            var (index, _, _) = await UniTask.WhenAny(closeTask, confirmTask);
            
            return index == 0
                ? new OneButtonModalResult(OneButtonModalCloseType.Cancel)
                : new OneButtonModalResult(OneButtonModalCloseType.Confirm);
        }
        
    }
}