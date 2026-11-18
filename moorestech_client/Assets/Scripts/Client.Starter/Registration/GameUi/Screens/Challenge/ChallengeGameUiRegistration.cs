using Client.Ui.Screens.Challenge.Tree;
using Client.Ui.State.Challenge.Commands;
using Client.Ui.State.Challenge.Tree;
using Client.Ui.State.Challenge.Tree.Selection;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi.Screens.Challenge
{
    // チャレンジ一覧の GameUi 登録（状態の源は MainGameUiStateRegistration）
    // Registers the challenge list GameUi (state source is MainGameUiStateRegistration)
    internal static class ChallengeGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.RegisterEntryPoint<ChallengeCategorySelectionStore>().AsSelf();
            builder.RegisterEntryPoint<ChallengeListModel>().AsSelf();
            builder.Register<ChallengeListCommands>(Lifetime.Singleton).As<IChallengeListCommands>();
            builder.RegisterEntryPoint<ChallengeListPresenter>();
        }
    }
}
