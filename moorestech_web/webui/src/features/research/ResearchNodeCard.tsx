import { Button, Paper, Stack, Text, Tooltip, Group } from "@mantine/core";
import type { ResearchNodeData } from "@/bridge/contract/payloadTypes";
import { ItemSlot } from "@/shared/ui";
import { dispatchAction } from "@/bridge";
import { deriveResearchButton, isItemSufficient } from "./researchLogic";
import styles from "./style.module.css";

type Props = {
  node: ResearchNodeData;
  left: number;
  top: number;
  owned: Map<number, number>;
  resolveName: (itemId: number) => string | undefined;
};

// 研究ノード表示と実行
// Render and complete one research node
export default function ResearchNodeCard({ node, left, top, owned, resolveName }: Props) {
  const button = deriveResearchButton(node, owned);
  return (
    <Paper
      withBorder
      p="xs"
      className={button.completed ? styles.nodeCompleted : styles.node}
      style={{ left, top }}
      data-testid={`research-node-${node.guid}`}
    >
      <Stack gap={4}>
        <Text size="sm" fw={600}>{node.name}</Text>
        <Text size="xs" c="dark.2" lineClamp={2}>{node.description}</Text>
        {node.consumeItems.length > 0 && (
          <Group gap={4}>
            {node.consumeItems.map((c, i) => (
              <Tooltip key={`${c.itemId}-${i}`} label={`${resolveName(c.itemId) ?? c.itemId} x${c.count}`}>
                <div>
                  <ItemSlot itemId={c.itemId} count={c.count} selected={isItemSufficient(node, c.itemId, c.count, owned)} />
                </div>
              </Tooltip>
            ))}
          </Group>
        )}
        {node.rewardItemIds.length + node.unlockItemIds.length > 0 && (
          <Group gap={4}>
            {[...node.rewardItemIds, ...node.unlockItemIds].map((id, i) => (
              <ItemSlot key={`${id}-${i}`} itemId={id} name={resolveName(id)} />
            ))}
          </Group>
        )}
        <Tooltip label={button.tooltip} multiline w={200} style={{ whiteSpace: "pre-line" }}>
          <div>
            <Button
              size="compact-xs"
              disabled={!button.interactable}
              data-testid={`research-button-${node.guid}`}
              onClick={() => void dispatchAction("research.complete", { researchGuid: node.guid })}
            >
              {button.completed ? "研究済み" : "研究"}
            </Button>
          </div>
        </Tooltip>
      </Stack>
    </Paper>
  );
}
