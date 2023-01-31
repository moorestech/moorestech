using MainGame.UnityView.UI.Builder.BluePrint;

namespace MainGame.UnityView.UI.Builder.Unity
{
    public interface IUIBuilderObject
    {
        public IUIBluePrintElement BluePrintElement { get; }
        public void Initialize(IUIBluePrintElement bluePrintElement);
    }
}