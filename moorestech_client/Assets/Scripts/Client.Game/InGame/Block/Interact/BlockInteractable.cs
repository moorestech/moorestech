using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using UnityEngine;

namespace Client.Game.InGame.Block.Interact
{
    /// <summary>
    ///     開けるブロックの面。付与も初期化もBlockGameObject.Initializeが行う
    ///     Interact face of an openable block; BlockGameObject.Initialize both attaches and initializes it
    /// </summary>
    public class BlockInteractable : MonoBehaviour, ITapInteractable
    {
        private BlockGameObject _blockGameObject;
        private GameObject _outlineObject;

        public GameObject GameObject => gameObject;
        public IReadOnlyList<ITapInteractAction> Actions { get; private set; } = Array.Empty<ITapInteractAction>();

        // 撤去済み（索引の墓標）とアクション未装填は候補から外す
        // A removed block (index tombstone) and an unstocked one both leave the candidates
        public bool IsInteractAvailable => 0 < Actions.Count && _blockGameObject.IsSearchable;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            Actions = new ITapInteractAction[] { new BlockOpenInteractAction(blockGameObject) };
        }

        public void SetHighlighted(bool highlighted)
        {
            RuntimeOutlineFactory.Apply(gameObject, ref _outlineObject, highlighted);
        }
    }
}
