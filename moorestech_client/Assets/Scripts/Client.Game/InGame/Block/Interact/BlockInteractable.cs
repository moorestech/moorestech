using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using Client.Localization;
using UniRx;
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

        // 撤去済み（索引の墓標）は候補から外す
        // A removed block (index tombstone) leaves the candidates
        public bool IsInteractAvailable => _blockGameObject.IsSearchable;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            var openAction = new BlockOpenInteractAction(blockGameObject);
            Actions = new ITapInteractAction[] { openAction };

            // 言語切替時にヒント名を再解決
            // Re-resolve and push the hint's block name whenever the language changes
            Localize.OnLanguageChanged.Subscribe(_ => openAction.RefreshHintParams()).AddTo(this);
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
