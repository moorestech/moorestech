import { useMemo } from "react";
import { CloseButton, Group, Paper, Title } from "@mantine/core";
import { useTopic, useTopicSelector, Topics, dispatchAction } from "@/bridge";
import { useItemMaster } from "@/bridge/store/useItemMaster";
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

  // grab と名前/maxStack 解決だけを memo 化した context 値。identity 安定で Body の無駄な再レンダーを防ぐ
  // Context value memoized to grab + name/maxStack resolution only; stable identity avoids needless Body re-renders
  const interaction = useMemo<BlockInteraction>(
    () => ({
      grabCount,
      resolveName: (itemId) => itemMaster?.get(itemId)?.name,
      resolveMaxStack: (itemId) => itemMaster?.get(itemId)?.maxStack,
    }),
    [grabCount, itemMaster],
  );

  // 未受信または閉状態なら何も描画しない（ブロック UI が開いていない）
  // Render nothing when not received or closed (no block UI is open)
  if (!data || !data.open) return null;

  // blockType に対応するコンポーネントを解決（未登録は GenericBlockInventory にフォールバック）
  // Resolve the component for blockType (unregistered falls back to GenericBlockInventory)
  const Body = resolveBlockComponent(data.blockType);

  return (
    <BlockInteractionContext.Provider value={interaction}>
      <Paper data-testid="block-inventory" className={styles.panel} p="md" withBorder bg="dark.6" c="dark.1">
        <Group justify="space-between" mb="sm">
          <Title order={2} size="h4">{data.blockName}</Title>
          {/* uGUIのEsc/Tab相当のマウス閉じ操作。GameScreenへの遷移をhostへ要求する */}
          {/* Mouse-driven close, like uGUI Esc/Tab; asks the host to transit to GameScreen */}
          <CloseButton
            data-testid="block-inventory-close"
            aria-label="close"
            onClick={() => {
              void dispatchAction("ui_state.request", { state: "GameScreen" });
            }}
          />
        </Group>
        <Body data={data} />
      </Paper>
    </BlockInteractionContext.Provider>
  );
}
