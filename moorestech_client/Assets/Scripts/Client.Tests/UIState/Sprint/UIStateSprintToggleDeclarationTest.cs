using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class UIStateSprintToggleDeclarationTest
    {
        [Test]
        public void ゲーム画面と設置と削除だけが走り反転を許可する()
        {
            // 全画面を列挙し、追加された宣言の見落としを検出する
            // Enumerate every screen to detect overlooked declarations
            var expected = new Dictionary<Type, bool>
            {
                { typeof(GameScreenState), true },
                { typeof(PlaceBlockState), true },
                { typeof(DeleteObjectState), true },
                { typeof(DebugBlockInfoState), false },
                { typeof(PlayerInventoryState), false },
                { typeof(SubInventoryState), false },
                { typeof(PauseMenuState), false },
                { typeof(ChallengeListState), false },
                { typeof(ResearchTreeState), false },
                { typeof(BuildMenuState), false },
                { typeof(TrainHUDScreenState), false },
                { typeof(SkitState), false },
            };
            var registered = typeof(UIStateDictionary).GetConstructors().Single()
                .GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.AreEqual(Enum.GetValues(typeof(UIStateEnum)).Length, registered.Length);
            CollectionAssert.AreEquivalent(expected.Keys, registered);

            // 宣言は依存を参照しないため、画面の構築を省いて全件を照合する
            // Declarations touch no dependencies, so compare all screens without constructing them
            foreach (var type in registered)
            {
                var state = (IUIState)FormatterServices.GetUninitializedObject(type);
                Assert.AreEqual(expected[type], state.AllowsSprintToggle(), type.Name);
            }
        }
    }
}
