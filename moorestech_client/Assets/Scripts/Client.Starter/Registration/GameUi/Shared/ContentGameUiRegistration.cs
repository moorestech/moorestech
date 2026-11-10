using Client.Ui.Content;
using VContainer;

namespace Client.Starter.Registration.GameUi.Shared
{
    // Content 層（ID → 表示素材）の登録。画面の Presenter・View が GameContentPresentation を受ける
    // Registers the Content layer (ID to display material); screen presenters and views receive GameContentPresentation
    internal static class ContentGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<ClientContextContentTextureSource>(Lifetime.Singleton).As<IGameContentTextureSource>();
            builder.Register<GameContentPresentation>(Lifetime.Singleton);
        }
    }
}
