using Client.Ui.State.Text;

namespace Client.Ui.State.Tutorial.KeyControlHint
{
    public sealed class KeyControlHintCue
    {
        public readonly string Key;
        public readonly string KeyName;
        public readonly UiText Text;

        public KeyControlHintCue(string key, string keyName, UiText text)
        {
            Key = key;
            KeyName = keyName;
            Text = text;
        }
    }
}
