using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public class OneButtonModalInstantiator : ModalInstantiatorBase
    {
        private const string AddressablePath = "Vanilla/UI/Modal/OneButtonModal";
        private readonly string _modalText;
        
        public OneButtonModalInstantiator(string modalText) : base(AddressablePath)
        {
            _modalText = modalText;
        }
        
        public override async UniTask<IModalObject> InstantiateModal()
        {
            var modalObject = await base.InstantiateModal();
            if (modalObject is not OneButtonModal oneButtonModal) throw new System.Exception($"ModalObject is not OneButtonModal. AddressablePath: {AddressablePath}");
            
            oneButtonModal.OneModalInitialize(_modalText);
            return oneButtonModal;
            
        }
    }
}