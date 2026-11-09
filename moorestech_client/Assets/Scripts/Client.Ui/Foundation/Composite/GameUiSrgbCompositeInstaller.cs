using System;
using Client.Ui.Foundation.Root;
using VContainer.Unity;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// GameUi の系でだけパネルを sRGB 合成の RT へ描かせ、画面の大きさを追う（A-180・A-183）
    /// Only in the GameUi system, draws the panel into the sRGB composite RT and follows the screen size (A-180, A-183)
    /// </summary>
    /// <remarks>
    /// Web の系では登録されないので RT も合成パスも作られない。パネルを差し替えるので、root.panel を控える入口より先に登録する
    /// Not registered in the Web system, so neither the RT nor the composite passes exist; it swaps the panel, so it is registered before entry points that keep root.panel
    /// </remarks>
    public sealed class GameUiSrgbCompositeInstaller : IInitializable, ITickable, IDisposable
    {
        private readonly GameUiRoot _gameUiRoot;
        private UiSrgbPanelTarget _panelTarget;

        public GameUiSrgbCompositeInstaller(GameUiRoot gameUiRoot)
        {
            _gameUiRoot = gameUiRoot;
        }

        public void Initialize()
        {
            _panelTarget = new UiSrgbPanelTarget(_gameUiRoot.GetPanelDocument());
        }

        // 画面の大きさの変化通知が無いので毎フレーム比べる（前例 ScreenFrameReader）
        // No screen-size change notification exists, so compare every frame (precedent ScreenFrameReader)
        public void Tick()
        {
            _panelTarget.SyncToScreenSize();
            UiSrgbCompositeSource.ReportWhenNotComposited();
        }

        public void Dispose()
        {
            _panelTarget.Dispose();
        }
    }
}
