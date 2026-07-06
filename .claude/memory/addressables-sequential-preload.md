---
name: addressables-sequential-preload
description: Addressablesは並列タスク内で同時ロードすると無限ハングする。クリティカル資産はInitializeAsync直後に直列プリロードする
metadata: 
  node_type: memory
  type: project
  originSessionId: 4af4fa5c-5b45-4d86-9cb7-4f7ba0604186
---

Addressables のロードは並列タスク内で同時に走らせると無限にハングすることがある（ブロックprefab大量ロードと ItemSlotView/FluidSlotView を同じ WhenAll に入れた時に発生）。

**Why:** 並列ロード時のバンドル競合。ChestBlockInventory のワークアラウンドロードは Dispose するとバンドル参照が失われるため保持し続ける必要がある。

**How to apply:**
- クリティカルな Addressables 資産は `Addressables.InitializeAsync()` 直後・並列タスク開始前に直列でプリロードする
- ChestBlockInventory のロードは Dispose しない（バンドル参照維持）
- ItemSlotView / FluidSlotView は並列 WhenAll から直列プリロードフェーズに移動済み
- パターンの実装: `InitializeScenePipeline.cs` の直列プリロードフェーズ（2026-07時点で89〜128行付近、`ChestBlockInventory` で検索）

関連: [[key-files]]
