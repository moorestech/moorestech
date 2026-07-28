import { Paper, Text } from "@mantine/core";
import type { ChallengeNodeData } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";
import { tutorialAnchor, challengeNodeAnchorId } from "@/shared/tutorialAnchor";

// 全状態をsource翻訳へ対応づける
// Map every state to source-string localization
const challengeStateLabelSource: Record<ChallengeNodeData["state"], string> = {
  locked: "未解放",
  current: "進行中",
  completed: "完了",
};

export default function ChallengeNodeCard({ node, left, top }: { node: ChallengeNodeData; left: number; top: number }) {
  const { t } = useI18n();

  // 拡縮・位置を保ち翻訳状態を描画する
  // Render the localized state while preserving scale and position
  return (
    <Paper className={`${styles.node} ${styles[node.state]}`} data-challenge-node
      data-testid={`challenge-node-${node.guid}`}
      {...tutorialAnchor(challengeNodeAnchorId(node.guid))}
      style={{ left, top, transform: `translate(-50%, -50%) scale(${node.scale.x}, ${node.scale.y})` }}>
      <Text fw={700}>{node.title}</Text>
      <Text size="sm">{node.summary}</Text>
      <Text size="xs">{t(challengeStateLabelSource[node.state])}</Text>
    </Paper>
  );
}
