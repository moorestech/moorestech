using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Core
{
    /// <summary>
    ///     列車車両のインタラクト面。F=車両インベントリ、E=乗車の2アクション
    ///     Interact face of a train car: F opens the car inventory, E boards it
    /// </summary>
    public class TrainCarInteractable : MonoBehaviour, ITapInteractable
    {
        private GameObject _outlineObject;

        public GameObject GameObject => gameObject;
        public IReadOnlyList<ITapInteractAction> Actions { get; private set; } = Array.Empty<ITapInteractAction>();

        // 車両は生成時に必ず2アクションを積むので、常にインタラクト対象になる
        // A car always carries its two actions from creation, so it is always a candidate
        public bool IsInteractAvailable => true;

        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            Actions = new ITapInteractAction[]
            {
                new TrainCarOpenInventoryInteractAction(trainCarEntityObject),
                new TrainCarRideInteractAction(trainCarEntityObject),
            };
        }

        public void SetHighlighted(bool highlighted)
        {
            // 初回ハイライト時だけ複製メッシュを作る
            // Build the duplicate mesh only on the first highlight
            if (highlighted && _outlineObject == null) _outlineObject = RuntimeOutlineFactory.Create(gameObject);
            if (_outlineObject != null) _outlineObject.SetActive(highlighted);
        }
    }
}
