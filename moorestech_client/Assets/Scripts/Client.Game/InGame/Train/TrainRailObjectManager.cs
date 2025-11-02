using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Network.API;
using Game.Train.Utility;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.Train
{
    public class TrainRailObjectManager : MonoBehaviour
    {
        [SerializeField] private Material splineMaterial;
        
        private const string ValidationLogPrefix = "[RailVisualization][Validation]";
        private const string ProtocolLogPrefix = "[RailVisualization][Protocol]";
        private static Material _sharedMaterial;
        
        private readonly Dictionary<RailConnectionKey, RailSplineComponent> _connectionToSpline = new();
        private readonly Dictionary<RailComponentKey, HashSet<RailConnectionKey>> _componentToConnections = new();
        
        [Inject] private BlockGameObjectDataStore _blockGameObjectDataStore;
        
        
        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            OnRailUpdateEvent(handshakeResponse.RailConnections, Array.Empty<RailComponentIDMessagePack>());
            ClientContext.VanillaApi.Event.SubscribeEventResponse(RailConnectionsEventPacket.EventTag, OnRailEvent);
        }
        
        public void OnRailDataReceived(RailConnectionDataMessagePack[] connections)
        {
            // 受信したレールデータで内部キャッシュを更新
            // Update cached rail data with the received connections
            if (connections == null || connections.Length == 0) return;
            
            var normalized = NormalizeConnections(connections);
            ApplyNormalizedConnections(normalized);
        }
        
        public void OnRailUpdateEvent(RailConnectionDataMessagePack[] allConnections, RailComponentIDMessagePack[] changedIds)
        {
            // サーバーイベントで届いた全量データを適用
            // Apply full snapshot received from the server event
            OnRailDataReceived(allConnections);
            foreach (var componentId in changedIds) UpdateRailComponentSpline(componentId);
        }
        
        public void UpdateRailComponentSpline(RailComponentIDMessagePack componentId)
        {
            // 指定コンポーネントに紐づくSplineを更新
            // Refresh splines linked to the specified rail component
            if (componentId == null) return;
            var componentKey = CreateComponentKey(componentId);
            if (!_componentToConnections.TryGetValue(componentKey, out var connectionKeys) || connectionKeys.Count == 0) return;
            var keysSnapshot = ListPool<RailConnectionKey>.Rent(connectionKeys.Count);
            keysSnapshot.AddRange(connectionKeys);
            foreach (var key in keysSnapshot)
            {
                if (!_connectionToSpline.TryGetValue(key, out var component)) continue;
                component.UpdateConnection(component.ConnectionData);
            }
            ListPool<RailConnectionKey>.Return(keysSnapshot);
        }
        
        private void OnRailEvent(byte[] payload)
        {
            // サーバーイベントをデシリアライズして処理
            // Deserialize event payload and process updates
            if (payload == null || payload.Length == 0)
            {
                Debug.LogWarning($"{ProtocolLogPrefix} Empty event payload received.");
                return;
            }
            
            var message = MessagePackSerializer.Deserialize<RailConnectionsEventPacket.RailConnectionsEventMessagePack>(payload);
            if (message == null)
            {
                Debug.LogError($"{ProtocolLogPrefix} Failed to deserialize rail connection event.");
                return;
            }
            
            var changed = message.ChangedComponentIds ?? Array.Empty<RailComponentIDMessagePack>();
            OnRailUpdateEvent(null, changed);
        }
        
        #region Internal
        
        
        private Dictionary<RailConnectionKey, RailConnectionDataMessagePack> NormalizeConnections(RailConnectionDataMessagePack[] connections)
        {
            // レール情報を正規化し重複や不正値を除去
            // Normalize incoming connections to remove duplicates and invalid entries
            var normalized = new Dictionary<RailConnectionKey, RailConnectionDataMessagePack>(connections.Length);
            foreach (var connection in connections)
            {
                if (!TryPrepareConnection(connection, out var key)) continue;
                if (normalized.ContainsKey(key)) continue;
                normalized[key] = connection;
            }
            return normalized;
        }
        
        private bool TryPrepareConnection(RailConnectionDataMessagePack connection, out RailConnectionKey key)
        {
            // 単一レール接続のバリデーションと整形
            // Validate and sanitize a single rail connection
            key = default;
            if (connection == null || connection.FromNode == null || connection.ToNode == null) return false;
            if (connection.FromNode.ComponentId == null || connection.ToNode.ComponentId == null) return false;
            if (!BezierRailCurveCalculator.ValidateRailConnection(connection.FromNode, connection.ToNode))
            {
                Debug.LogWarning($"{ValidationLogPrefix} Invalid control point data detected, skipping connection.");
                return false;
            }
            
            if (connection.Distance < 0)
            {
                connection.Distance = Math.Abs(connection.Distance);
                Debug.LogWarning($"{ValidationLogPrefix} Negative distance detected, using absolute value.");
            }
            
            key = CreateConnectionKey(connection);
            return true;
        }
        
        private void ApplyNormalizedConnections(Dictionary<RailConnectionKey, RailConnectionDataMessagePack> normalized)
        {
            // 正規化済みデータとの差分を適用
            // Apply normalized snapshot by reconciling differences
            RemoveMissingConnections(normalized);
            var material = ResolveMaterial();
            foreach (var pair in normalized)
            {
                if (_connectionToSpline.TryGetValue(pair.Key, out var component))
                {
                    component.UpdateConnection(pair.Value);
                    continue;
                }
                
                var splineComponent = CreateSplineComponent(pair.Value, material);
                if (splineComponent == null) continue;
                splineComponent.gameObject.SetActive(true);
                
                _connectionToSpline[pair.Key] = splineComponent;
                RegisterComponentIndex(pair.Key);
            }
        }
        
        private void RemoveMissingConnections(Dictionary<RailConnectionKey, RailConnectionDataMessagePack> normalized)
        {
            // 受信データに無い接続を破棄
            // Destroy splines that are no longer present in the snapshot
            if (_connectionToSpline.Count == 0) return;
            var removalList = ListPool<RailConnectionKey>.Rent(_connectionToSpline.Count);
            foreach (var key in _connectionToSpline.Keys)
            {
                if (normalized.ContainsKey(key)) continue;
                removalList.Add(key);
            }
            
            foreach (var key in removalList)
            {
                if (!_connectionToSpline.TryGetValue(key, out var component)) continue;
                if (component != null) Destroy(component.gameObject);
                _connectionToSpline.Remove(key);
                UnregisterComponentIndex(key);
            }
            ListPool<RailConnectionKey>.Return(removalList);
        }
        
        private void RemoveAllConnections()
        {
            // レール情報が空の場合に全破棄
            // Destroy all splines when snapshot is empty
            if (_connectionToSpline.Count == 0) return;
            foreach (var component in _connectionToSpline.Values)
            {
                if (component != null) Destroy(component.gameObject);
            }
            _connectionToSpline.Clear();
            _componentToConnections.Clear();
        }
        
        private void RegisterComponentIndex(RailConnectionKey key)
        {
            // コンポーネントと接続の対応表を作成
            // Map component identifiers to connection keys
            AddConnectionMapping(key.From, key);
            AddConnectionMapping(key.To, key);
        }
        
        private void UnregisterComponentIndex(RailConnectionKey key)
        {
            // コンポーネントと接続の対応表から削除
            // Remove mapping from component to connection keys
            RemoveConnectionMapping(key.From, key);
            RemoveConnectionMapping(key.To, key);
        }
        
        private void AddConnectionMapping(RailComponentKey componentKey, RailConnectionKey connectionKey)
        {
            // コンポーネントキーへの接続登録
            // Register connection key onto the component map
            if (!_componentToConnections.TryGetValue(componentKey, out var set))
            {
                set = new HashSet<RailConnectionKey>();
                _componentToConnections[componentKey] = set;
            }
            set.Add(connectionKey);
        }
        
        private void RemoveConnectionMapping(RailComponentKey componentKey, RailConnectionKey connectionKey)
        {
            // 対応表から特定接続を除去
            // Remove specific connection key from component map
            if (!_componentToConnections.TryGetValue(componentKey, out var set)) return;
            set.Remove(connectionKey);
            if (set.Count == 0) _componentToConnections.Remove(componentKey);
        }
        
        private RailSplineComponent CreateSplineComponent(RailConnectionDataMessagePack connection, Material material)
        {
            // 新たなRailSplineComponentを生成
            // Create a new rail spline component
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var component = go.AddComponent<RailSplineComponent>();
            // レールの端点BlockGameObjectを解決
            // Resolve BlockGameObjects located at both ends of the rail
            var startBlock = ResolveBlockGameObject(connection.FromNode);
            var endBlock = ResolveBlockGameObject(connection.ToNode);
            component.Initialize(connection, material, startBlock, endBlock);
            return component;
        }
        
        private BlockGameObject ResolveBlockGameObject(RailNodeInfoMessagePack node)
        {
            // RailNodeに対応するBlockGameObjectを取得
            // Fetch the BlockGameObject that corresponds to the provided rail node
            if (node == null || node.ComponentId == null) return null;
            var position = (Vector3Int)node.ComponentId.Position;
            return _blockGameObjectDataStore != null && _blockGameObjectDataStore.TryGetBlockGameObject(position, out var blockGameObject) ? blockGameObject : null;
        }
        
        private static RailComponentKey CreateComponentKey(RailComponentIDMessagePack componentId)
        {
            // レールコンポーネントIDからキーを生成
            // Generate dictionary key from component identifier
            var position = (Vector3Int)componentId.Position;
            return new RailComponentKey(position, componentId.ID);
        }
        
        private static RailConnectionKey CreateConnectionKey(RailConnectionDataMessagePack connection)
        {
            // 接続固有のキーを生成
            // Create a unique key for the rail connection
            var fromKey = CreateComponentKey(connection.FromNode.ComponentId);
            var toKey = CreateComponentKey(connection.ToNode.ComponentId);
            return new RailConnectionKey(fromKey, connection.FromNode.IsFrontSide, toKey, connection.ToNode.IsFrontSide);
        }
        
        private Material ResolveMaterial()
        {
            // 表示用マテリアルを取得
            // Resolve material used for spline rendering
            if (splineMaterial != null) return splineMaterial;
            if (_sharedMaterial != null) return _sharedMaterial;
            
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning($"{ValidationLogPrefix} Failed to find URP Unlit shader for rail visualization.");
                return null;
            }
            
            _sharedMaterial = new Material(shader) { color = new Color(0.8f, 0.85f, 0.95f, 0.8f) };
            return _sharedMaterial;
        }
        
        private readonly struct RailComponentKey : IEquatable<RailComponentKey>
        {
            public RailComponentKey(Vector3Int position, int id)
            {
                Position = position;
                Id = id;
            }
            
            public Vector3Int Position { get; }
            public int Id { get; }
            
            public bool Equals(RailComponentKey other)
            {
                return Position == other.Position && Id == other.Id;
            }
            
            public override bool Equals(object obj)
            {
                return obj is RailComponentKey other && Equals(other);
            }
            
            public override int GetHashCode()
            {
                return HashCode.Combine(Position, Id);
            }
        }
        
        private readonly struct RailConnectionKey : IEquatable<RailConnectionKey>
        {
            public RailConnectionKey(RailComponentKey from, bool fromFront, RailComponentKey to, bool toFront)
            {
                From = from;
                FromFront = fromFront;
                To = to;
                ToFront = toFront;
            }
            
            public RailComponentKey From { get; }
            public bool FromFront { get; }
            public RailComponentKey To { get; }
            public bool ToFront { get; }
            
            public bool Equals(RailConnectionKey other)
            {
                return From.Equals(other.From) && FromFront == other.FromFront && To.Equals(other.To) && ToFront == other.ToFront;
            }
            
            public override bool Equals(object obj)
            {
                return obj is RailConnectionKey other && Equals(other);
            }
            
            public override int GetHashCode()
            {
                return HashCode.Combine(From, FromFront, To, ToFront);
            }
        }
        
        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();
            
            public static List<T> Rent(int capacity)
            {
                // 一時リストをプールから取得
                // Rent a temporary list from the pool
                if (Pool.Count == 0) return new List<T>(capacity);
                var list = Pool.Pop();
                list.Capacity = Math.Max(list.Capacity, capacity);
                return list;
            }
            
            public static void Return(List<T> list)
            {
                // 使用済みリストをプールへ返却
                // Return the temporary list back to the pool
                list.Clear();
                Pool.Push(list);
            }
        }
        
        #endregion
    }
}
