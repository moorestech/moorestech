# Entity Synchronization Architecture - Comprehensive Overview

## 1. ENTITY SYSTEM ARCHITECTURE

### 1.1 Core Entity Interfaces (Server-Side)

**File:** `/moorestech_server/Assets/Scripts/Game.Entity.Interface/IEntity.cs`

```csharp
public interface IEntity
{
    EntityInstanceId InstanceId { get; }
    string EntityType { get; }
    Vector3 Position { get; }
    string State { get; }
    void SetPosition(Vector3 serverVector3);
}
```

**Key Points:**
- Entities have a unique `EntityInstanceId` (long value wrapped by UnitGenerator)
- Entity types are string identifiers (e.g., "VanillaItem", "VanillaPlayer")
- State is a string representation of entity-specific data (e.g., for items: "itemId,count")

### 1.2 Entity Instance Types

**PlayerEntity** (`/moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityInstance/PlayerEntity.cs`)
- Implements `IEntity`
- Holds player position synchronized from client
- Empty state (returns `string.Empty`)

**ItemEntity** (`/moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityInstance/ItemEntity.cs`)
- Implements `IEntity`
- Represents items on conveyor belts
- State: "itemId,count" (e.g., "123,45")
- Has `SetState(IItemStack)` and `SetState(ItemId, int count)` methods

### 1.3 Entity Identifier

**EntityInstanceId** (`/moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityInstanceId.cs`)
- Wrapped `long` value using UnitGenerator
- Type-safe wrapper around raw long IDs
- Can convert to primitive: `instanceId.AsPrimitive()`

### 1.4 Factory Pattern

**EntityFactory** (`/moorestech_server/Assets/Scripts/Game.Entity/EntityFactory.cs`)
```csharp
public class EntityFactory : IEntityFactory
{
    public IEntity CreateEntity(string entityType, EntityInstanceId instanceId, Vector3 position = default)
    public IEntity LoadEntity(string entityType, EntityInstanceId instanceId, Vector3 serverPosition = default)
}
```

**Supported Entity Types:**
- `VanillaEntityType.VanillaPlayer` → `PlayerEntity`
- `VanillaEntityType.VanillaItem` → `ItemEntity`

---

## 2. SERVER-SIDE ENTITY MANAGEMENT

### 2.1 Entity Data Store

**File:** `/moorestech_server/Assets/Scripts/Game.Entity/EntitiesDatastore.cs`

```csharp
public class EntitiesDatastore : IEntitiesDatastore
{
    private readonly Dictionary<EntityInstanceId, IEntity> _entities = new();
    
    public void Add(IEntity entity);
    public bool Exists(EntityInstanceId instanceId);
    public IEntity Get(EntityInstanceId instanceId);
    public void SetPosition(EntityInstanceId instanceId, Vector3 position);
    public Vector3 GetPosition(EntityInstanceId instanceId);
    public List<EntityJsonObject> GetSaveJsonObject();
    public void LoadBlockDataList(List<EntityJsonObject> saveBlockDataList);
}
```

**Responsibilities:**
- Stores all entities in a dictionary keyed by `EntityInstanceId`
- Manages entity CRUD operations
- Handles save/load functionality for persistence

---

## 3. SERVER-TO-CLIENT ENTITY SYNCHRONIZATION

### 3.1 Message Pack Format

**File:** `/moorestech_server/Assets/Scripts/Server.Util/MessagePack/EntityMessagePack.cs`

```csharp
[MessagePackObject]
public class EntityMessagePack
{
    [Key(0)] public long InstanceId { get; set; }
    [Key(1)] public string Type { get; set; }
    [Key(2)] public Vector3MessagePack Position { get; set; }
    [Key(3)] public string State { get; set; }
    
    // Constructor from IEntity
    public EntityMessagePack(IEntity entity)
    {
        InstanceId = entity.InstanceId.AsPrimitive();
        Type = entity.EntityType;
        State = entity.State;
        Position = new Vector3MessagePack(entity.Position);
    }
}
```

### 3.2 Initial World Data Protocol

**File:** `/moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestWorldDataProtocol.cs`

**Protocol Tag:** `"va:getWorldData"`

**Flow:**
1. Client requests world data (blocks + entities)
2. Server collects:
   - All blocks from `WorldBlockDatastore`
   - All entities from belt conveyors using `CollectBeltConveyorItems.CollectItemFromWorld()`
3. Returns `ResponseWorldDataMessagePack` containing:
   - `BlockDataMessagePack[]` array
   - `EntityMessagePack[]` array

**Current Limitation:** Only belt conveyor items are currently returned as entities. Note in code indicates future support for "true entities" is planned.

### 3.3 Entity Response Data Model

**File:** `/moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs`

```csharp
public class EntityResponse
{
    public readonly long InstanceId;
    public readonly Vector3 Position;
    public readonly string State;
    public readonly string Type;
    
    public EntityResponse(EntityMessagePack entityMessagePack)
    {
        InstanceId = entityMessagePack.InstanceId;
        Type = entityMessagePack.Type;
        Position = entityMessagePack.Position;
        State = entityMessagePack.State;
    }
}

public class WorldDataResponse
{
    public readonly List<BlockInfo> Blocks;
    public readonly List<EntityResponse> Entities;
}
```

---

## 4. PROTOCOL & EVENT SYSTEMS

### 4.1 Event Protocol (Polling-Based Updates)

**File:** `/moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/EventProtocol.cs`

**Protocol Tag:** `"va:event"`

```csharp
[MessagePackObject]
public class EventProtocolMessagePack : ProtocolMessagePackBase
{
    [Key(2)] public int PlayerId { get; set; }
}

[MessagePackObject]
public class ResponseEventProtocolMessagePack : ProtocolMessagePackBase
{
    [Key(2)] public List<EventMessagePack> Events { get; set; }
}
```

**EventMessagePack Structure:**
```csharp
[MessagePackObject]
public class EventMessagePack
{
    [Key(0)] public string Tag { get; set; }  // Event identifier (e.g., "va:event:blockPlace")
    [Key(1)] public byte[] Payload { get; set; }  // Serialized event data
}
```

### 4.2 Event Management (Server-Side)

**File:** `/moorestech_server/Assets/Scripts/Server.Event/EventProtocolProvider.cs`

```csharp
public class EventProtocolProvider
{
    private readonly Dictionary<int, List<EventMessagePack>> _events = new();
    
    public void AddEvent(int playerId, string tag, byte[] payload);
    public void AddBroadcastEvent(string tag, byte[] payload);
    public List<EventMessagePack> GetEventBytesList(int playerId);
}
```

**Event Flow:**
1. Various event packets (e.g., `PlaceBlockEventPacket`) call `AddEvent()` or `AddBroadcastEvent()`
2. Events are queued per player or broadcast to all players
3. Client polls via `EventProtocol` to fetch queued events
4. Events are cleared from server after retrieval

### 4.3 Event Collection (Client-Side)

**File:** `/moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiEvent.cs`

```csharp
public class VanillaApiEvent
{
    private readonly Dictionary<string, Subject<byte[]>> _eventResponseSubjects = new();
    
    private async UniTask CollectEvent()
    {
        while (true)
        {
            var request = new EventProtocolMessagePack(_playerConnectionSetting.PlayerId);
            var response = await _packetExchangeManager.GetPacketResponse<ResponseEventProtocolMessagePack>(request, ct);
            
            foreach (var eventMessagePack in response.Events)
            {
                if (!_eventResponseSubjects.TryGetValue(eventMessagePack.Tag, out var subjects)) continue;
                subjects.OnNext(eventMessagePack.Payload);  // Dispatch to subscribers
            }
            
            await UniTask.Delay(ServerConst.PollingRateMillSec, cancellationToken: ct);
        }
    }
    
    public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
}
```

**Polling Strategy:**
- Client continuously polls at `ServerConst.PollingRateMillSec` interval
- Uses UniRx `Subject<byte[]>` for event dispatching
- Subscribers receive deserialized payloads

---

## 5. CLIENT-SIDE ENTITY RENDERING

### 5.1 Entity Object Interface

**File:** `/moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/IEntityObject.cs`

```csharp
public interface IEntityObject
{
    public long EntityId { get; }
    public void Initialize(long entityId);
    public void SetDirectPosition(Vector3 position);
    public void SetInterpolationPosition(Vector3 position);
    public void Destroy();
}
```

### 5.2 Item Entity Object (Visual Representation)

**File:** `/moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/ItemEntityObject.cs`

```csharp
public class ItemEntityObject : MonoBehaviour, IEntityObject
{
    public long EntityId { get; private set; }
    
    // Position interpolation
    private float _linerTime;
    private Vector3 _previousPosition;
    private Vector3 _targetPosition;
    
    private void Update()
    {
        // Linear interpolation between positions
        var rate = _linerTime / NetworkConst.UpdateIntervalSeconds;
        rate = Mathf.Clamp01(rate);
        transform.position = Vector3.Lerp(_previousPosition, _targetPosition, rate);
        _linerTime += Time.deltaTime;
    }
    
    public void SetDirectPosition(Vector3 position);  // Immediate positioning
    public void SetInterpolationPosition(Vector3 position);  // Smooth interpolation
}
```

**Features:**
- Smooth position interpolation between server updates
- Uses `NetworkConst.UpdateIntervalSeconds` for interpolation duration
- Material management for item textures

### 5.3 Entity Data Store (Client)

**File:** `/moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs`

```csharp
public class EntityObjectDatastore : MonoBehaviour
{
    [SerializeField] private ItemEntityObject itemPrefab;
    private readonly Dictionary<long, (DateTime lastUpdate, IEntityObject objectEntity)> _entities = new();
    
    public void OnEntitiesUpdate(List<EntityResponse> entities)
    {
        foreach (var entity in entities)
            if (_entities.ContainsKey(entity.InstanceId))
            {
                // Existing entity: Update position with interpolation
                _entities[entity.InstanceId].objectEntity.SetInterpolationPosition(entity.Position);
                _entities[entity.InstanceId] = (DateTime.Now, _entities[entity.InstanceId].objectEntity);
            }
            else
            {
                // New entity: Create and initialize
                var entityObject = CreateEntity(entity);
                entityObject.Initialize(entity.InstanceId);
                _entities.Add(entity.InstanceId, (DateTime.Now, entityObject));
            }
    }
    
    private IEntityObject CreateEntity(EntityResponse entity)
    {
        if (entity.Type == VanillaEntityType.VanillaItem)
        {
            var item = Instantiate(itemPrefab, entity.Position, Quaternion.identity, transform);
            
            // Parse state: "itemId,count"
            var id = new ItemId(int.Parse(entity.State.Split(',')[0]));
            var viewData = ClientContext.ItemImageContainer.GetItemView(id);
            Texture texture = null;
            if (viewData != null)
            {
                texture = viewData.ItemTexture;
            }
            
            item.SetTexture(texture);
            return item;
        }
        
        throw new ArgumentException("エンティティタイプがありません");
    }
    
    // Automatic cleanup: Entities expire after 1 second of no updates
    private void Update()
    {
        var removeEntities = new List<long>();
        foreach (var entity in _entities)
            if ((DateTime.Now - entity.Value.lastUpdate).TotalSeconds > 1)
                removeEntities.Add(entity.Key);
        foreach (var removeEntity in removeEntities)
        {
            _entities[removeEntity].objectEntity.Destroy();
            _entities.Remove(removeEntity);
        }
    }
}
```

**Key Features:**
- Tracks last update timestamp per entity
- Auto-destroys entities that haven't been updated in 1 second
- Handles spawn/despawn of entity GameObjects
- Parses entity state to extract visual data (item textures)

---

## 6. FULL INTEGRATION: WORLD DATA HANDLER

**File:** `/moorestech_client/Assets/Scripts/Client.Game/InGame/World/WorldDataHandler.cs`

```csharp
public class WorldDataHandler : IInitializable
{
    private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
    private readonly EntityObjectDatastore _entitiesDatastore;
    
    public WorldDataHandler(BlockGameObjectDataStore blockGameObjectDataStore, 
                            EntityObjectDatastore entitiesDatastore, 
                            InitialHandshakeResponse initialHandshakeResponse)
    {
        _blockGameObjectDataStore = blockGameObjectDataStore;
        _entitiesDatastore = entitiesDatastore;
        
        // Subscribe to block events
        ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, OnBlockUpdate);
        ClientContext.VanillaApi.Event.SubscribeEventResponse(RemoveBlockToSetEventPacket.EventTag, OnBlockRemove);
        
        ApplyWorldData(initialHandshakeResponse.WorldData);
    }
    
    public void Initialize()
    {
        UpdateWorldData().Forget();  // Continuous polling loop
    }
    
    private async UniTask UpdateWorldData()
    {
        while (true)
        {
            var data = await ClientContext.VanillaApi.Response.GetWorldData(ct);
            if (data != null)
            {
                ApplyWorldData(data);
            }
            
            await UniTask.Delay(NetworkConst.UpdateIntervalMilliseconds, cancellationToken: ct);  // ~500ms
        }
    }
    
    private void ApplyWorldData(WorldDataResponse worldData)
    {
        foreach (var block in worldData.Blocks)
            PlaceBlock(block.BlockPos, block.BlockId, block.BlockDirection);
        
        if (worldData.Entities == null)
            return;
        
        _entitiesDatastore.OnEntitiesUpdate(worldData.Entities);  // Update entities!
    }
}
```

---

## 7. SYNCHRONIZATION FLOW DIAGRAM

```
SERVER SIDE:
=============
Game State (Entities, Blocks)
    ↓
EntityFactory / EntitiesDatastore
    ↓
RequestWorldDataProtocol.GetResponse()
    ├→ Collects EntityMessagePack[] from EntitiesDatastore
    └→ Collects BlockDataMessagePack[] from WorldBlockDatastore
    ↓
ResponseWorldDataMessagePack sent to client via GetWorldData()

POLLING INTERVAL: ~500ms (NetworkConst.UpdateIntervalMilliseconds)


CLIENT SIDE:
============
WorldDataHandler.UpdateWorldData() polls GetWorldData()
    ↓
VanillaApiWithResponse.GetWorldData() → RequestWorldDataProtocol
    ↓
Receives ResponseWorldDataMessagePack
    ↓
WorldDataHandler.ApplyWorldData(WorldDataResponse)
    ├→ Updates blocks via BlockGameObjectDataStore
    └→ Updates entities via EntityObjectDatastore.OnEntitiesUpdate()
        ├→ New entities: Instantiate ItemEntityObject prefab
        ├→ Existing entities: SetInterpolationPosition() for smooth movement
        └→ Timeout: Remove entities not updated in >1 second
```

---

## 8. ENTITY SPAWN/DESPAWN HANDLING

### 8.1 Entity Spawn (Client)
1. Entity appears in `WorldDataResponse.Entities` from server
2. `EntityObjectDatastore.OnEntitiesUpdate()` detects new entity ID
3. Calls `CreateEntity()` which instantiates `ItemEntityObject` prefab
4. Initializes with entity ID and textures from item master data
5. Tracks last update time

### 8.2 Entity Despawn (Client)
1. **Automatic Timeout:** Entities not received in >1 second are removed
2. Update loop in `EntityObjectDatastore.Update()` checks all entities
3. Calls `objectEntity.Destroy()` which destroys the GameObject
4. Removes from tracking dictionary

### 8.3 Entity Creation (Server)
1. `EntityFactory.CreateEntity()` called with type and ID
2. Creates `ItemEntity` or `PlayerEntity` instance
3. Added to `EntitiesDatastore`
4. Included in next world data sync

---

## 9. KEY CLASSES & FILE LOCATIONS

| Component | File Path |
|-----------|-----------|
| **IEntity** (Interface) | `moorestech_server/Assets/Scripts/Game.Entity.Interface/IEntity.cs` |
| **PlayerEntity** | `moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityInstance/PlayerEntity.cs` |
| **ItemEntity** | `moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityInstance/ItemEntity.cs` |
| **EntityInstanceId** | `moorestech_server/Assets/Scripts/Game.Entity.Interface/EntityInstanceId.cs` |
| **EntityFactory** | `moorestech_server/Assets/Scripts/Game.Entity/EntityFactory.cs` |
| **EntitiesDatastore** | `moorestech_server/Assets/Scripts/Game.Entity/EntitiesDatastore.cs` |
| **EntityMessagePack** | `moorestech_server/Assets/Scripts/Server.Util/MessagePack/EntityMessagePack.cs` |
| **RequestWorldDataProtocol** | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RequestWorldDataProtocol.cs` |
| **EventProtocol** | `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/EventProtocol.cs` |
| **EventProtocolProvider** | `moorestech_server/Assets/Scripts/Server.Event/EventProtocolProvider.cs` |
| **IEntityObject** (Interface) | `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/IEntityObject.cs` |
| **ItemEntityObject** | `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/ItemEntityObject.cs` |
| **EntityObjectDatastore** | `moorestech_client/Assets/Scripts/Client.Game/InGame/Entity/EntityObjectDatastore.cs` |
| **EntityResponse** | `moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs` |
| **VanillaApiEvent** | `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiEvent.cs` |
| **WorldDataHandler** | `moorestech_client/Assets/Scripts/Client.Game/InGame/World/WorldDataHandler.cs` |

---

## 10. SYNCHRONIZATION INTERVALS

- **World Data Poll Interval:** `NetworkConst.UpdateIntervalMilliseconds` (~500ms)
- **Event Poll Interval:** `ServerConst.PollingRateMillSec`
- **Entity Timeout (Auto-Despawn):** 1 second of no updates
- **Position Interpolation Duration:** `NetworkConst.UpdateIntervalSeconds`

---

## 11. STATE MANAGEMENT FOR ENTITIES

Currently, entity state is stored as a **string representation**:

### Item Entity State Format:
```
"itemId,count"
Example: "123,45" (item ID 123, stack size 45)
```

### Player Entity State Format:
```
Empty string (string.Empty)
```

**Parsing Example (Client):**
```csharp
var id = new ItemId(int.Parse(entity.State.Split(',')[0]));
var count = int.Parse(entity.State.Split(',')[1]);
```

---

## 12. NOTES & FUTURE IMPROVEMENTS

1. **Limited Entity Types:** Currently only supports `VanillaItem` and `VanillaPlayer`. Framework supports extensibility.

2. **Belt Conveyor Specific:** Currently only belt conveyor items are collected as entities. Code comment indicates this is temporary:
   ```csharp
   //TODO 今はベルトコンベアのアイテムをエンティティとして返しているだけ 今後は本当のentityも返す
   ```

3. **No Event-Based Entity Updates:** Entity updates come via polling world data, not through event protocol. This is different from block updates which use event protocol.

4. **Simple Timeout Mechanism:** Entities disappear if not received in 1 second. No prediction or interpolation beyond position smoothing.

5. **Thread Safety:** `EventProtocolProvider` uses `lock()` for thread-safe event queue management.

6. **Extensibility Points:**
   - Add new entity types via `VanillaEntityType` and `EntityFactory.CreateEntity()`
   - Implement `IEntityObject` for new visual representations
   - Extend `EntityMessagePack` with additional fields as needed
