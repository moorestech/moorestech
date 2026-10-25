using Client.Game.InGame.UI.Challenge;
using Client.Network.API;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // チャレンジの全カテゴリがWebホスト無しでもDIの状態に載っていることを確かめる
    // Verifies every challenge category is held by the DI state without the web host
    internal static class ChallengeStateProbe
    {
        public static void AssertAvailable(IObjectResolver resolver)
        {
            var state = resolver.Resolve<ChallengeProgressState>();
            var handshake = resolver.Resolve<InitialHandshakeResponse>();
            Assert.IsNotNull(state.Categories, "チャレンジの状態がDIに無い");
            Assert.AreEqual(handshake.Challenges.Count, state.Categories.Count);
        }
    }
}
