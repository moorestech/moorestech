using Client.Localization;
using Client.Ui.Foundation.Root;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 言語変更に合わせてルートのロケールのクラスを付け替え、フォントの先頭を切り替える
    /// Re-applies the root locale class on language change to switch the leading font
    /// </summary>
    public class GameUiLocaleClassBinder : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;

        public GameUiLocaleClassBinder(GameUiRoot gameUiRoot)
        {
            _gameUiRoot = gameUiRoot;
        }

        public void Initialize()
        {
            Apply();

            // 源は static で PlayMode を抜けても残るので、購読はルートの GameObject の破棄で解く
            // The static source survives leaving play mode, so the subscription ends when the root GameObject is destroyed
            Localize.OnLanguageChanged.Subscribe(_ => Apply()).AddTo(_gameUiRoot);
        }

        private void Apply()
        {
            _gameUiRoot.SetRootClassEnabled(GameUiLocaleClass.CjkClassName, GameUiLocaleClass.IsCjk(Localize.GetCurrentLanguageCode()));
        }
    }
}
