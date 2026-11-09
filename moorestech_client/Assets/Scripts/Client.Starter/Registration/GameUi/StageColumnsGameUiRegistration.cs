using Client.Ui.Foundation.Stage.Columns;
using VContainer;
using VContainer.Unity;

namespace Client.Starter.Registration.GameUi
{
    // 舞台の上段 3 列（GameUi）の登録。画面は StageColumnsLayout を受けて列へ載せる
    // Registers the GameUi top-row stage columns; screens receive StageColumnsLayout and add into a column
    internal static class StageColumnsGameUiRegistration
    {
        public static void Register(ContainerBuilder builder)
        {
            builder.Register<StageColumnsLayout>(Lifetime.Singleton);
            builder.RegisterEntryPoint<StageColumnsMount>();
        }
    }
}
