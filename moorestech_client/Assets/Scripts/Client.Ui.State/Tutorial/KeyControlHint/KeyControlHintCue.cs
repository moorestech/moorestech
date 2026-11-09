using Client.Ui.State.Text;

namespace Client.Ui.State.Tutorial.KeyControlHint
{
    public sealed class KeyControlHintCue
    {
        public readonly string KeyName;
        public readonly UiText Text;

        public KeyControlHintCue(string keyName, UiText text)
        {
            KeyName = keyName;
            Text = text;
        }
    }
}
