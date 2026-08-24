using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Network.API;
using CommandForgeGenerator.Command;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Map.MapObject
{
    /// <summary>
    ///     mapObjectを実行時生成し、破壊とHPを同期する
    ///     Instantiates map objects at runtime and synchronizes destroy and HP state
    /// </summary>
    public class MapObjectGameObjectDatastore : MonoBehaviour, ISkitWorldObjectControl, IMapObjectPinTargetSource
    {
        private readonly MapObjectRegistry _registry = new();
        private readonly ReactiveProperty<bool> _isWorldObjectActive = new(true);
        private MapObjectInstantiationRunner _instantiationRunner;

        public IReadOnlyReactiveProperty<bool> IsNearFieldInstantiated => _instantiationRunner.IsNearFieldInstantiated;
        public IReadOnlyReactiveProperty<bool> IsAllInstantiated => _instantiationRunner.IsAllInstantiated;

        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // 購読確定後に生成を始める
            // Settle event subscription synchronously before instantiation starts
            ClientContext.VanillaApi.Event.SubscribeEventResponse(MapObjectUpdateEventPacket.EventTag, OnUpdateMapObject);

            // 初期データをrunnerへ束ねる
            // Bind snapshots and layouts into the instantiation runner
            var snapshotByInstanceId = handshakeResponse.MapObjects.ToDictionary(info => info.InstanceId);
            var instantiator = new MapObjectLayoutInstantiator(transform, _registry, snapshotByInstanceId);
            var nearFieldOrder = MapObjectLayoutDistanceOrder.SortNearFieldFirst(
                handshakeResponse.MapLayout.MapObjects, handshakeResponse.PlayerPos);

            _instantiationRunner = new MapObjectInstantiationRunner(
                instantiator,
                nearFieldOrder,
                _registry,
                _isWorldObjectActive,
                this.GetCancellationTokenOnDestroy());
            _instantiationRunner.StartNearFieldInstantiation();
        }

        internal UniTask WaitForNearFieldInstantiationAsync()
        {
            return _instantiationRunner.WaitForNearFieldInstantiationAsync();
        }

        public void StartBackgroundInstantiation()
        {
            _instantiationRunner.StartBackgroundInstantiation();
        }

        private void OnUpdateMapObject(byte[] payLoad)
        {
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(payLoad);

            // 生成済み判定と保留を登録簿へ閉じ込める
            // Keep instantiated checks and pending state inside the registry
            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    _registry.ApplyDestroy(data.InstanceId);
                    break;
                case MapObjectUpdateEventMessagePack.HpUpdateEventType:
                    _registry.ApplyHp(data.InstanceId, data.CurrentHp);
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }

        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);

            // 実状態へ揃えてから生成loopへ通知する
            // Notify the loop after matching the actual state
            _isWorldObjectActive.Value = enable;
        }

        public MapObjectGameObject SearchNearestMapObject(HashSet<Guid> mapObjectGuids, Vector3 position)
        {
            return _registry.SearchNearest(mapObjectGuids, position);
        }
    }

    public interface IMapObjectPinTargetSource
    {
        IReadOnlyReactiveProperty<bool> IsAllInstantiated { get; }
        MapObjectGameObject SearchNearestMapObject(HashSet<Guid> mapObjectGuids, Vector3 position);
    }
}
