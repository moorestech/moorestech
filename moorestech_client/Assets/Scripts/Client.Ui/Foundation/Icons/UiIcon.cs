using Client.Ui.State.Text;
using UnityEngine;

namespace Client.Ui.Foundation.Icons
{
    /// <summary>
    /// アイコン 1 つ分の表示素材
    /// Display material for one icon
    /// </summary>
    public sealed class UiIcon
    {
        // 外部ロードの結果なので null を許す（AGENTS.md の null 規約の例外側。null は GameIcon が代わりの文字にする）
        // An external load result, so null is allowed (the exception side of the AGENTS.md null rule; GameIcon turns null into fallback text)
        public Texture2D Texture { get; }
        public string IdText { get; }
        public UiText Label { get; }

        public UiIcon(Texture2D texture, string idText, UiText label)
        {
            Texture = texture;
            IdText = idText;
            Label = label;
        }
    }
}
