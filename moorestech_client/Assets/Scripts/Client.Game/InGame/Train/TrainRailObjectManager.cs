using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Network.API;
using Game.Train.Utility;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using VContainer;
using RailConnectionData = Server.Util.MessagePack.RailConnectionMessagePack.RailConnectionData;
using RailComponentIdPack = Server.Util.MessagePack.RailConnectionMessagePack.RailComponentIDMessagePack;

namespace Client.Game.InGame.Train
{
    public interface ITrainRailObjectManager
    {
        void Initialize();
        void Dispose();
        void OnRailDataReceived(RailConnectionData[] connections);
        void OnRailUpdateEvent(RailConnectionData[] allConnections, RailComponentIdPack[] changedIds);
        void SetVisualizationEnabled(bool enabled);
        void UpdateRailComponentSpline(RailComponentIdPack componentId);
    }
    
    public class TrainRailObjectManager : MonoBehaviour, ITrainRailObjectManager
    {
        private const string ValidationLogPrefix = "[RailVisualization][Validation]";
        private const string ProtocolLogPrefix = "[RailVisualization][Protocol]";
        private static Material _sharedMaterial;
        
        private readonly Dictionary<RailConnectionKey, RailSplineRecord> _connectionToSpline = new();
        private readonly Dictionary<RailComponentKey, HashSet<RailConnectionKey>> _componentToConnections = new();
        
        [SerializeField] private Material splineMaterial;
        
        private IDisposable _eventSubscription;
        private bool _hasInitialized;
        private bool _visualizationEnabled = true;
        private RailConnectionData[] _pendingConnections = Array.Empty<RailConnectionData>();
        
        public void Initialize()
        {
            // イベント購読と初期データ適用の起点
            // Subscribe to events and apply any pending data
            if (_hasInitialized) return;
            if (!TrySubscribeEvent()) return;
            _hasInitialized = true;
            if (_pendingConnections.Length == 0) return;
            OnRailDataReceived(_pendingConnections);
            _pendingConnections = Array.Empty<RailConnectionData>();
        }
        
        public void Dispose()
        {
            // 作成済みSplineと購読を解放
            // Clean up generated spline objects and subscriptions
            foreach (var record in _connectionToSpline.Values)
            {
                if (record.Container != null) Destroy(record.Container.gameObject);
            }
            _connectionToSpline.Clear();
            _componentToConnections.Clear();
            _eventSubscription?.Dispose();
            _eventSubscription = null;
            _hasInitialized = false;
        }
        
        public void OnRailDataReceived(RailConnectionData[] connections)
        {
            // 受信したレールデータで内部キャッシュを更新
            // Update cached rail data with the received connections
            if (connections == null || connections.Length == 0)
            {
                RemoveAllConnections();
                return;
            }
            
            var normalized = NormalizeConnections(connections);
            ApplyNormalizedConnections(normalized);
        }
        
        public void OnRailUpdateEvent(RailConnectionData[] allConnections, RailComponentIdPack[] changedIds)
        {
            // サーバーイベントで届いた全量データを適用
            // Apply full snapshot received from the server event
            OnRailDataReceived(allConnections);
            if (changedIds == null || changedIds.Length == 0) return;
            foreach (var componentId in changedIds) UpdateRailComponentSpline(componentId);
        }
        
        public void SetVisualizationEnabled(bool enabled)
        {
            // 描画有効状態を切り替え
            // Toggle spline visualization on or off
            _visualizationEnabled = enabled;
            foreach (var record in _connectionToSpline.Values)
            {
                if (record.Container == null) continue;
                record.Container.gameObject.SetActive(enabled);
            }
        }
        
        public void UpdateRailComponentSpline(RailComponentIdPack componentId)
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
                if (!_connectionToSpline.TryGetValue(key, out var record)) continue;
                ApplySplineGeometry(record, record.Data);
            }
            ListPool<RailConnectionKey>.Return(keysSnapshot);
        }
        
        private void OnEnable()
        {
            // 有効化時に初期化を試行
            // Attempt initialization when enabled
            Initialize();
        }
        
        private void Update()
        {
            // 初期化が未完ならリトライ
            // Retry initialization until dependencies are ready
            if (_hasInitialized) return;
            Initialize();
        }
        
        private void OnDestroy()
        {
            // 破棄時に後処理を実行
            // Execute cleanup when destroyed
            Dispose();
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
            
            var connections = message.AllConnections ?? Array.Empty<RailConnectionData>();
            var changed = message.ChangedComponentIds ?? Array.Empty<RailComponentIdPack>();
            OnRailUpdateEvent(connections, changed);
        }
        
        #region Internal
        
        [Inject]
        public void Construct(InitialHandshakeResponse handshakeResponse)
        {
            // ハンドシェイク時に受け取った初期データを保持
            // Preserve initial rail snapshot from handshake
            _pendingConnections = handshakeResponse.RailConnections ?? Array.Empty<RailConnectionData>();
            Initialize();
        }
        
        private bool TrySubscribeEvent()
        {
            // VanillaApiが利用可能か確認して購読を設定
            // Subscribe to event stream once VanillaApi is available
            if (ClientContext.VanillaApi == null)
            {
                Debug.LogWarning($"{ProtocolLogPrefix} VanillaApi is not ready, retrying initialization.");
                return false;
            }
            
            _eventSubscription?.Dispose();
            _eventSubscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RailConnectionsEventPacket.EventTag, OnRailEvent);
            return true;
        }
        
        private Dictionary<RailConnectionKey, RailConnectionData> NormalizeConnections(RailConnectionData[] connections)
        {
            // レール情報を正規化し重複や不正値を除去
            // Normalize incoming connections to remove duplicates and invalid entries
            var normalized = new Dictionary<RailConnectionKey, RailConnectionData>(connections.Length);
            foreach (var connection in connections)
            {
                if (!TryPrepareConnection(connection, out var key)) continue;
                if (normalized.ContainsKey(key)) continue;
                normalized[key] = connection;
            }
            return normalized;
        }
        
        private bool TryPrepareConnection(RailConnectionData connection, out RailConnectionKey key)
        {
            // 単一レール接続のバリデーションと整形
            // Validate and sanitize a single rail connection
            key = default;
            if (connection == null || connection.FromNode == null || connection.ToNode == null) return false;
            if (connection.FromNode.ComponentId == null || connection.ToNode.ComponentId == null) return false;
            SanitizeControlPoint(connection.FromNode.ControlPoint, "FromNode");
            SanitizeControlPoint(connection.ToNode.ControlPoint, "ToNode");
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
        
        private void ApplyNormalizedConnections(Dictionary<RailConnectionKey, RailConnectionData> normalized)
        {
            // 正規化済みデータとの差分を適用
            // Apply normalized snapshot by reconciling differences
            RemoveMissingConnections(normalized);
            foreach (var pair in normalized)
            {
                if (_connectionToSpline.TryGetValue(pair.Key, out var record))
                {
                    record.Data = pair.Value;
                    ApplySplineGeometry(record, record.Data);
                    continue;
                }
                
                var container = CreateSplineContainer(pair.Value);
                if (container == null) continue;
                container.gameObject.SetActive(_visualizationEnabled);
                
                var newRecord = new RailSplineRecord(pair.Value, container);
                _connectionToSpline[pair.Key] = newRecord;
                RegisterComponentIndex(pair.Key);
            }
        }
        
        private void RemoveMissingConnections(Dictionary<RailConnectionKey, RailConnectionData> normalized)
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
                if (!_connectionToSpline.TryGetValue(key, out var record)) continue;
                if (record.Container != null) Destroy(record.Container.gameObject);
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
            foreach (var record in _connectionToSpline.Values)
            {
                if (record.Container != null) Destroy(record.Container.gameObject);
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
        
        private SplineContainer CreateSplineContainer(RailConnectionData connection)
        {
            // 新たなSplineContainerを生成し制御点を設定
            // Create a new spline container and configure control points
            var go = new GameObject($"RailSpline_{FormatComponent(connection.FromNode.ComponentId)}_{FormatComponent(connection.ToNode.ComponentId)}");
            go.transform.SetParent(transform, false);
            var container = go.AddComponent<SplineContainer>();
            ApplySplineGeometry(new RailSplineRecord(connection, container), connection);
            
            var extrude = go.AddComponent<SplineExtrude>();
            extrude.Container = container;
            extrude.SegmentsPerUnit = 4f;
            extrude.Sides = 6;
            extrude.Radius = 0.08f;
            extrude.Capped = false;
            
            var renderer = go.GetComponent<MeshRenderer>();
            var material = ResolveMaterial();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            
            return container;
        }
        
        private void ApplySplineGeometry(RailSplineRecord record, RailConnectionData connection)
        {
            // 制御点計算とSplineへの反映
            // Calculate control points and update the spline geometry
            var spline = record.Container?.Spline;
            if (spline == null) return;
            var controlPoints = BezierRailCurveCalculator.CalculateBezierControlPoints(connection.FromNode, connection.ToNode, connection.Distance);
            spline.Clear();
            var startKnot = new BezierKnot((float3)controlPoints.p0, float3.zero, (float3)(controlPoints.p1 - controlPoints.p0));
            var endKnot = new BezierKnot((float3)controlPoints.p3, (float3)(controlPoints.p2 - controlPoints.p3), float3.zero);
            spline.Add(startKnot);
            spline.Add(endKnot);
        }
        
        private static void SanitizeControlPoint(RailConnectionMessagePack.RailControlPointMessagePack controlPoint, string label)
        {
            // 制御点の不正値を補正
            // Sanitize control point data to avoid invalid vectors
            if (controlPoint == null) return;
            var original = SanitizeVector((Vector3)controlPoint.OriginalPosition, label, "OriginalPosition");
            var tangent = SanitizeVector((Vector3)controlPoint.ControlPointPosition, label, "ControlPointPosition");
            controlPoint.OriginalPosition = new Vector3MessagePack(original);
            controlPoint.ControlPointPosition = new Vector3MessagePack(tangent);
        }
        
        private static Vector3 SanitizeVector(Vector3 vector, string context, string field)
        {
            // NaNやInfinityを検出してゼロへ補正
            // Clamp NaN or Infinity vectors to zero
            if (float.IsNaN(vector.x) || float.IsNaN(vector.y) || float.IsNaN(vector.z) ||
                float.IsInfinity(vector.x) || float.IsInfinity(vector.y) || float.IsInfinity(vector.z))
            {
                Debug.LogWarning($"{ValidationLogPrefix} Invalid vector detected in {context}.{field}, defaulting to zero.");
                return Vector3.zero;
            }
            return vector;
        }
        
        private static RailComponentKey CreateComponentKey(RailComponentIdPack componentId)
        {
            // レールコンポーネントIDからキーを生成
            // Generate dictionary key from component identifier
            var position = (Vector3Int)componentId.Position;
            return new RailComponentKey(position, componentId.ID);
        }
        
        private static RailConnectionKey CreateConnectionKey(RailConnectionData connection)
        {
            // 接続固有のキーを生成
            // Create a unique key for the rail connection
            var fromKey = CreateComponentKey(connection.FromNode.ComponentId);
            var toKey = CreateComponentKey(connection.ToNode.ComponentId);
            return new RailConnectionKey(fromKey, connection.FromNode.IsFrontSide, toKey, connection.ToNode.IsFrontSide);
        }
        
        private static string FormatComponent(RailComponentIdPack componentId)
        {
            // ログ出力用にコンポーネント情報を整形
            // Format component identifier for logging
            var pos = (Vector3Int)componentId.Position;
            return $"{pos.x}_{pos.y}_{pos.z}_{componentId.ID}";
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
        
        private sealed class RailSplineRecord
        {
            public RailSplineRecord(RailConnectionData data, SplineContainer container)
            {
                Data = data;
                Container = container;
            }
            
            public RailConnectionData Data { get; set; }
            public SplineContainer Container { get; }
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
