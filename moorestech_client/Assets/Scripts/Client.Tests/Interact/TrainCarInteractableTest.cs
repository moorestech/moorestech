using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact
{
    /// <summary>
    ///     列車インタラクト面（F=車両インベントリ / E=乗車）のアクション構成と実行結果を検証
    ///     Verifies the train car interact face's action layout and execution results (F = car inventory, E = ride)
    /// </summary>
    public class TrainCarInteractableTest
    {
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects) Object.DestroyImmediate(createdObject);
            _createdObjects.Clear();
        }

        [Test]
        public void 列車は車両インベントリと乗車の2アクションを持つ()
        {
            var interactable = CreateTrainCarInteractable();

            Assert.IsTrue(interactable.IsInteractAvailable);
            Assert.AreEqual(2, interactable.Actions.Count);
            Assert.AreSame(InputManager.Playable.Interact, interactable.Actions[0].Key);
            Assert.AreSame(InputManager.Playable.Ride, interactable.Actions[1].Key);
        }

        [Test]
        public void Fアクションは車両インベントリを開く()
        {
            var interactable = CreateTrainCarInteractable();

            var transit = interactable.Actions[0].Execute();

            Assert.AreEqual(UIStateEnum.SubInventory, transit.TransitContext.NextStateEnum);
            Assert.IsInstanceOf<TrainSubInventorySource>(transit.TransitContext.GetContext<ISubInventorySource>());
        }

        [Test]
        public void Eアクションは乗車リクエストを発行する()
        {
            var trainCarEntityObject = CreateTrainCarEntityObject();
            var interactable = AttachTrainCarInteractable(trainCarEntityObject);

            var transit = interactable.Actions[1].Execute();

            Assert.AreEqual(UIStateEnum.TrainHUDScreen, transit.TransitContext.NextStateEnum);
            var request = transit.TransitContext.GetContext<RideTrainCarRequest>();
            Assert.AreEqual(trainCarEntityObject.TrainCarInstanceId, request.TargetCarId);
        }

        // TrainCarObjectFactory.CreateTrainEntityと同じ手順（Rigidbody付きGameObject→Initialize→インタラクト面付与）を最小構成で再現する
        // Reproduces TrainCarObjectFactory.CreateTrainEntity's steps (Rigidbody GameObject -> Initialize -> attach interact face) at minimum scale
        private TrainCarInteractable CreateTrainCarInteractable()
        {
            return AttachTrainCarInteractable(CreateTrainCarEntityObject());
        }

        private static TrainCarInteractable AttachTrainCarInteractable(TrainCarEntityObject trainCarEntityObject)
        {
            var interactable = trainCarEntityObject.gameObject.AddComponent<TrainCarInteractable>();
            interactable.Initialize(trainCarEntityObject);
            return interactable;
        }

        private TrainCarEntityObject CreateTrainCarEntityObject()
        {
            var gameObject = new GameObject(nameof(TrainCarEntityObject));
            _createdObjects.Add(gameObject);
            gameObject.AddComponent<Rigidbody>();

            var trainCarEntityObject = gameObject.AddComponent<TrainCarEntityObject>();
            trainCarEntityObject.Initialize(TrainCarInstanceId.Create(), null);
            return trainCarEntityObject;
        }
    }
}
