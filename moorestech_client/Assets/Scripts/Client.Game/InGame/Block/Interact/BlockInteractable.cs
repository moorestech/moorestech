using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using UnityEngine;

namespace Client.Game.InGame.Block.Interact
{
    /// <summary>
    ///     開けるブロックのインタラクト面。開けないブロックは候補にならない
    ///     Interact face of an openable block; non-openable blocks never become candidates
    /// </summary>
    public class BlockInteractable : MonoBehaviour, ITapInteractable
    {
        private static readonly IReadOnlyList<ITapInteractAction> NoActions = Array.Empty<ITapInteractAction>();

        private BlockGameObject _blockGameObject;
        private GameObject _outlineObject;

        public GameObject GameObject => gameObject;
        public IReadOnlyList<ITapInteractAction> Actions { get; private set; } = NoActions;

        // 撤去済み（索引の墓標）は候補から外す
        // A removed block (index tombstone) leaves the candidates
        public bool IsInteractAvailable => Actions.Count > 0 && _blockGameObject.IsSearchable;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            if (blockGameObject.BlockMasterElement.IsBlockOpenable())
                Actions = new ITapInteractAction[] { new BlockOpenInteractAction(blockGameObject) };
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
