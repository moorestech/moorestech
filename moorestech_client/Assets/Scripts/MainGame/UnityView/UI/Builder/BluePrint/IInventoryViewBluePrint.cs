using System.Collections.Generic;
using MainGame.UnityView.UI.Builder.Element;

namespace MainGame.UnityView.UI.Builder.BluePrint
{
    /// <summary>
    ///     実際にどんな要素を表示するかを定義するブループリントのinterface
    /// </summary>
    public interface IInventoryViewBluePrint
    {
        public List<IUIBluePrintElement> Elements { get; }
    }
}