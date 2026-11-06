// moorestech_client/Assets/Scripts/Client.Ui/Components/Text/ShortcutHintLabel/ShortcutHintLayout.cs
namespace Client.Ui.Components.Text
{
    /// <summary>
    /// kbd の置き方。Prefix は常に先頭、Inline は文中の印の位置（Web の LocalizedShortcutHint の layout）
    /// Where the kbd goes: Prefix always first, Inline at the marker in the sentence (Web LocalizedShortcutHint layout)
    /// </summary>
    public enum ShortcutHintLayout
    {
        Prefix,
        Inline,
    }
}
