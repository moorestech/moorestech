---
name: chest-pushes-items-every-tick
description: VanillaChestComponent pushes ALL items to connected IBlockInventory targets every tick — adjacent receivers ping-pong unless they reject backflow
metadata: 
  node_type: memory
  type: project
  originSessionId: 8030a30f-b9a0-4762-8952-e3633b025c6f
---

`VanillaChestComponent.Update()` は毎tick、全スロットのアイテムを `ConnectingInventoryListPriorityInsertItemService` 経由で接続先 `IBlockInventory` へ押し出す（チェストは「貯める」のではなく「押し出す」ブロック）。

**Why:** チェストの隣に「受けて返す」タイプの IBlockInventory（中継器など）を置くと、アイテムが両者間を毎tick往復し、テストのアサート時点でどちらに居るかが更新順依存になる。CleanRoomItemHatch はこれで一度テストが落ちた。

**How to apply:** 中継系コンポーネントは `InsertItemContext.SourceBlockInstanceId` を自分の `BlockConnectorComponent.ConnectedTargets` の `TargetBlock.BlockInstanceId` と照合して逆流を拒否する（`context.SourceConnector != null` のときだけ判定。Empty contextはdefault idのため判定対象外）。実装例: `CleanRoomItemHatchComponent.InsertItem`（CleanRoomブランチ上の実装、原則自体はブロック共通）。
