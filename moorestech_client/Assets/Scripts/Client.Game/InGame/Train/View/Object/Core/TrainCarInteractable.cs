using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Core
{
    /// <summary>
    ///     列車車両のインタラクト面。F=車両インベントリ、E=乗車
    ///     Interact face of a train car: F opens the car inventory, E boards it
    /// </summary>
    public class TrainCarInteractable : MonoBehaviour, ITapInteractable
    {
        private GameObject _outlineObject;

        public GameObject GameObject => gameObject;
        public IReadOnlyList<ITapInteractAction> Actions { get; private set; } = Array.Empty<ITapInteractAction>();

        // アクションが積まれる前（Initialize前）は候補にしない
        // Before Initialize stocks the actions there is nothing to do, so it is not a candidate
        public bool IsInteractAvailable => 0 < Actions.Count;

        internal void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            Actions = new ITapInteractAction[]
            {
                new TrainCarOpenInventoryInteractAction(trainCarEntityObject),
                new TrainCarRideInteractAction(trainCarEntityObject),
            };
        }

        public void SetHighlighted(bool highlighted)
        {
            RuntimeOutlineFactory.Apply(gameObject, ref _outlineObject, highlighted);
        }
    }
}
