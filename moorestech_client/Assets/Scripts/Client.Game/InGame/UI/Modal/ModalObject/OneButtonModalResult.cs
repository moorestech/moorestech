// [uGUI廃止Phase1] Web UI移行済みのため未メンテ・描画恒久停止。Phase2で削除予定（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] Unmaintained; rendering permanently disabled after the Web UI migration. Slated for deletion in Phase2 (docs/webui/ugui-retirement-plan.md)
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