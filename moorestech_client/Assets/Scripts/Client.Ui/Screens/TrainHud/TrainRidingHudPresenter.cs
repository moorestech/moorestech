using Client.Ui.Foundation.Layers;
using Client.Ui.Foundation.Root;
using Client.Ui.Foundation.Stage;
using Client.Ui.State.Train;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Screens.TrainHud
{
    /// <summary>
    /// 列車乗車 HUD を起動時に舞台の Screen 層へ載せ、Model の変化を View へ押す
    /// Mounts the train riding HUD on the stage Screen layer at startup and pushes Model changes to the View
    /// </summary>
    public sealed class TrainRidingHudPresenter : IInitializable
    {
        private readonly GameUiRoot _gameUiRoot;
        private readonly TrainRidingHudModel _model;
        private TrainRidingHudView _view;

        public TrainRidingHudPresenter(GameUiRoot gameUiRoot, TrainRidingHudModel model)
        {
            _gameUiRoot = gameUiRoot;
            _model = model;
        }

        public void Initialize()
        {
            // Web は stage の中の fixed・z stage-screen
            // On the Web it is fixed inside the stage at z stage-screen
            _view = new TrainRidingHudView();
            _gameUiRoot.GetLayer(UiLayer.Screen, UiFamily.Stage).Add(_view);
            _model.Current.Subscribe(_view.Render);
        }

    }
}
