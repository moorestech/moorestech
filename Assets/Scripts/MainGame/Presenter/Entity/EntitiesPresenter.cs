using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.UnityView.Entity;
using UnityEngine;
using VContainer;

namespace MainGame.Presenter.Entity
{
    public class EntitiesPresenter : MonoBehaviour
    {
        [SerializeField] private ItemEntityObject itemPrefab;

        [Inject]
        public void Construct(ReceiveEntitiesDataEvent receiveEntitiesDataEvent)
        {
            receiveEntitiesDataEvent.OnEntitiesUpdate += OnEntitiesUpdate;
        }

        private void OnEntitiesUpdate(List<EntityProperties> entities)
        {
            
        }
    }
}