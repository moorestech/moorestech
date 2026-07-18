/* eslint-disable local/no-jsx-visible-literal -- Server-localized master strings and CSS/test IDs are dynamic data, not hard-coded copy. */
import { Paper, Text } from "@mantine/core";
import type { ChallengeNodeData } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

export default function ChallengeNodeCard({ node, left, top }: { node: ChallengeNodeData; left: number; top: number }) {
  const { t } = useI18n();
  return (
    <Paper className={`${styles.node} ${styles[node.state]}`} data-challenge-node
      data-testid={`challenge-node-${node.guid}`}
      style={{ left, top, transform: `translate(-50%, -50%) scale(${node.scale.x}, ${node.scale.y})` }}>
      <Text fw={700}>{node.title}</Text>
      <Text size="sm">{node.summary}</Text>
      <Text size="xs">{t(`challenge.state.${node.state}`)}</Text>
    </Paper>
  );
}
