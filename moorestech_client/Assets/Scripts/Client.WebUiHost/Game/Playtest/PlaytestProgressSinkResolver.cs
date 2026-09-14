using Client.Game.InGame.Playtest.Progress;
using VContainer;

namespace Client.WebUiHost.Game.Playtest
{
    // 進行記録の窓口をDIから取り出す唯一の場所。登録が外れていても action 登録全体を道連れにせず記録だけ縮退させる
    // The only place the progress window is pulled from DI; a missing registration degrades the recording alone instead of taking every action registration down
    public static class PlaytestProgressSinkResolver
    {
        public static IPlaytestProgressSink Resolve(IObjectResolver resolver)
        {
            if (resolver is IScopedObjectResolver scoped && !scoped.TryGetRegistration(typeof(IPlaytestProgressSink), out _)) return NullPlaytestProgressSink.Instance;
            return resolver.Resolve<IPlaytestProgressSink>();
        }
    }
}
