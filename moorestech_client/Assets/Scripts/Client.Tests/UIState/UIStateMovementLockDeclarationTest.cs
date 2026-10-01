using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.NestedPause;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class UIStateMovementLockDeclarationTest
    {
        [Test]
        public void ポーズメニューだけが自機の移動を止めると宣言している()
        {
            // 定数を返す画面の全件表。画面を足すと突き合わせが必ず落ちて宣言の見直しを強制する
            // The full table for constant-returning screens; adding a screen always fails the match and forces a review
            var expectedDeclarations = new Dictionary<Type, bool>
            {
                { typeof(GameScreenState), false },
                { typeof(PlaceBlockState), false },
                { typeof(DeleteObjectState), false },
                { typeof(DebugBlockInfoState), false },
                { typeof(PlayerInventoryState), false },
                { typeof(SubInventoryState), false },
                { typeof(PauseMenuState), true },
                { typeof(ChallengeListState), false },
                { typeof(ResearchTreeState), false },
                { typeof(BuildMenuState), false },
            };

            // 画面の集合はUIStateDictionaryの登録が正。列挙と個数が揃っていることを先に確かめる
            // UIStateDictionary's registration is the authority for the screen set; check its count matches the enum first
            var registeredStateTypes = typeof(UIStateDictionary).GetConstructors().Single()
                .GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.AreEqual(Enum.GetValues(typeof(UIStateEnum)).Length, registeredStateTypes.Length, "画面の数が列挙と合っていない");

            // 入れ子ポーズを持つ画面の宣言はサブステートへの委譲なので、定数を返す画面だけをここで読む
            // Screens owning a nested pause delegate their declaration, so only the constant-returning screens are read here
            var declarations = registeredStateTypes
                .Where(type => !typeof(INestedPauseScreenState).IsAssignableFrom(type))
                .ToDictionary(type => type, type => ((IUIState)FormatterServices.GetUninitializedObject(type)).LocksPlayerMovement());

            CollectionAssert.AreEquivalent(expectedDeclarations.Keys, declarations.Keys, "画面が増減した。新しい画面の移動可否をこのテストへ反映すること");
            CollectionAssert.AreEquivalent(
                expectedDeclarations.Where(pair => pair.Value).Select(pair => pair.Key),
                declarations.Where(pair => pair.Value).Select(pair => pair.Key));
        }

        [Test]
        public void 入れ子サブステートはポーズと列車操作中だけ移動を止めると宣言している()
        {
            var expectedLockingSubStates = new[]
            {
                "PauseMenuNestedSubState",
                "TrainHudGameScreenSubState",
            };

            // サブステートの宣言は依存を使わない定数。全実装をリフレクションで集めて宣言を読む
            // Sub-state declarations are constants that touch no dependency, so every implementation is collected and read
            var subStateTypes = typeof(INestedPauseSubState).Assembly.GetTypes()
                .Where(type => typeof(INestedPauseSubState).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
                .ToArray();
            var declarations = subStateTypes.ToDictionary(
                type => type.Name,
                type => ((INestedPauseSubState)FormatterServices.GetUninitializedObject(type)).LocksPlayerMovement());

            // 全実装を並べて突き合わせるので、サブステートを増やすとこのテストが必ず落ちる
            // Every implementation is matched as a set, so adding a sub-state always fails this test
            CollectionAssert.AreEquivalent(
                new[] { "PauseMenuNestedSubState", "TrainHudGameScreenSubState", "SkitGameScreenSubState" },
                declarations.Keys,
                "サブステートが増減した。新しいサブステートの移動可否をこのテストへ反映すること");
            CollectionAssert.AreEquivalent(expectedLockingSubStates, declarations.Where(pair => pair.Value).Select(pair => pair.Key));
        }
    }
}
