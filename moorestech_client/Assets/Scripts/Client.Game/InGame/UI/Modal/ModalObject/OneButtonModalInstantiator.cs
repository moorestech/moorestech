using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public class OneButtonModalInstantiator : ModalInstantiatorBase
    {
        private const string AddressablePath = "Vanilla/UI/Modal/OneButtonModal";
        private readonly OneButtonModalProperties _properties;
        
        public OneButtonModalInstantiator(OneButtonModalProperties properties) : base(AddressablePath)
        {
            _properties = properties;
        }
        
        public override async UniTask<IModalObject> InstantiateModal()
        {
            var modalObject = await base.InstantiateModal();
            if (modalObject is not OneButtonModal oneButtonModal) throw new System.Exception($"ModalObject is not OneButtonModal. AddressablePath: {AddressablePath}");
            
            oneButtonModal.OneModalInitialize(_properties);
            return oneButtonModal;
            
        }
    }
    
    public class OneButtonModalProperties
    {
        public readonly string Title;
        public readonly string Description;
        public readonly string ButtonText;
        public readonly bool ButtonInteractable;
        
        public OneButtonModalProperties(string title, string description, string buttonText, bool buttonInteractable)
        {
            Title = title;
            Description = description;
            ButtonText = buttonText;
            ButtonInteractable = buttonInteractable;
        }
    }
}