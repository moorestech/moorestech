namespace Client.Game.InGame.UI.Modal.ModalObject
{
    public class OneButtonModalResult : IModalResult
    {
        public OneButtonModalCloseType CloseType { get; }
        
        public OneButtonModalResult(OneButtonModalCloseType closeType)
        {
            CloseType = closeType;
        }
    }
    
    public enum OneButtonModalCloseType
    {
        Cancel,
        Confirm,
    }
}