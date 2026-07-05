import { Paper, Title } from "@mantine/core";
import { useTopic, useTopicSelector, dispatchAction, Topics } from "@/bridge";
import type { ActionPayloads } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { resolveBlockComponent } from "./blockLogic";
import { BlockInteractionContext, type BlockInteraction } from "./blockInteractionContext";
import styles from "./style.module.css";

// ブロック UI のオーバーレイ。uGUI の SubInventoryState 相当で、blockType から中身を静的解決する
// Block UI overlay; the SubInventoryState equivalent, statically resolving the body from blockType
export default function BlockInventoryPanel() {
  const data = useTopic(Topics.blockInventory);
  // grab.count しか使わないためセレクタ購読にし、他インベントリ更新での再レンダーを避ける
  // Only grab.count is used, so subscribe via a selector to avoid re-rendering on other inventory updates
  const grabCount = useTopicSelector(Topics.inventory, (inv) => inv?.grab.count ?? 0);
  const itemMaster = useItemMaster();

  // 未受信または閉状態なら何も描画しない（ブロック UI が開いていない）
  // Render nothing when not received or closed (no block UI is open)
  if (!data || !data.open) return null;

  // grab/名前/送信を context へまとめ、{data} 固定 contract の登録コンポーネントへ供給する
  // Bundle grab/name/dispatch into context and supply them to registered {data}-contract components
  const interaction: BlockInteraction = {
    grabCount,
    resolveName: (itemId) => itemMaster?.get(itemId)?.name,
    dispatch: (payload: ActionPayloads["block_inventory.move_item"]) =>
      void dispatchAction("block_inventory.move_item", payload),
  };

  // blockType に対応するコンポーネントを解決（未登録は GenericBlockInventory にフォールバック）
  // Resolve the component for blockType (unregistered falls back to GenericBlockInventory)
  const Body = resolveBlockComponent(data.blockType);

  return (
    <BlockInteractionContext.Provider value={interaction}>
      <Paper data-testid="block-inventory" className={styles.panel} p="md" withBorder bg="dark.6" c="dark.1">
        <Title order={2} size="h4" mb="sm">{data.blockName}</Title>
        <Body data={data} />
      </Paper>
    </BlockInteractionContext.Provider>
  );
}
