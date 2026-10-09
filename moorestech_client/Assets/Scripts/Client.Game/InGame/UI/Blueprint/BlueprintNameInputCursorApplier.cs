using Client.Game.InGame.UI.UIState.State.CameraPolicy;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    ///     BP名入力の開閉をカーソルポリシーへプッシュし、FPS建築中でもモーダルをクリック可能にする
    ///     Pushes the blueprint-name input open state to the cursor policy so the modal is clickable even in FPS build mode
    /// </summary>
    public class BlueprintNameInputCursorApplier : IInitializable
    {
        private readonly BlueprintNameInputState _nameInputState;
        private readonly UiStateCameraPolicyService _cameraPolicyService;

        public BlueprintNameInputCursorApplier(BlueprintNameInputState nameInputState, UiStateCameraPolicyService cameraPolicyService)
        {
            _nameInputState = nameInputState;
            _cameraPolicyService = cameraPolicyService;
        }

        public void Initialize()
        {
            _nameInputState.OnOpenChanged.Subscribe(_cameraPolicyService.SetBuildModalOpen);
        }
    }
}
