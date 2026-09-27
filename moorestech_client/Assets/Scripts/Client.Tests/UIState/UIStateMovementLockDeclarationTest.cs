using System.Linq;
using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState.State;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class UIStateMovementLockDeclarationTest
    {
        [Test]
        public void メニュー画面だけが自機の移動を止めると宣言している()
        {
            var expectedMenuStates = new[]
            {
                typeof(PlayerInventoryState),
                typeof(SubInventoryState),
                typeof(PauseMenuState),
                typeof(ChallengeListState),
                typeof(ResearchTreeState),
                typeof(BuildMenuState),
            };

            // 宣言は定数を返すだけなので、依存を組まずに生成して全画面を読む
            // Declarations only return constants, so build every screen without dependencies and read them all
            var stateTypes = typeof(IUIState).Assembly.GetTypes()
                .Where(type => typeof(IUIState).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                .ToArray();
            var lockingStates = stateTypes
                .Where(type => ((IUIState)FormatterServices.GetUninitializedObject(type)).LocksPlayerMovement())
                .ToArray();

            Assert.AreEqual(12, stateTypes.Length, "画面の数が変わった。新しい画面の移動可否をこのテストへ反映すること");
            CollectionAssert.AreEquivalent(expectedMenuStates, lockingStates);
        }
    }
}
