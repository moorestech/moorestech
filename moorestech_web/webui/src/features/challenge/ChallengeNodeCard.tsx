import { Paper, Text } from "@mantine/core";
import type { ChallengeNodeData } from "@/bridge";
import {
  challengeSummaryKey,
  challengeTitleKey,
  L,
  useI18n,
  type TranslationKey,
} from "@/shared/i18n";
import styles from "./style.module.css";
import { tutorialAnchor, challengeNodeAnchorId } from "@/shared/tutorialAnchor";

export default function ChallengeNodeCard({ node, left, top }: { node: ChallengeNodeData; left: number; top: number }) {
  const { t } = useI18n();

  // 拡縮・位置を保ち翻訳状態を描画する
  // Render the localized state while preserving scale and position
  return (
    <Paper className={`${styles.node} ${styles[node.state]}`}
      data-testid={`challenge-node-${node.guid}`}
      {...tutorialAnchor(challengeNodeAnchorId(node.guid))}
      style={{ left, top, transform: `translate(-50%, -50%) scale(${node.scale.x}, ${node.scale.y})` }}>
      <Text fw={700}>{t(challengeTitleKey(node.guid))}</Text>
      <Text size="sm">{t(challengeSummaryKey(node.guid))}</Text>
      <Text size="xs">{t(resolveChallengeStateKey(node.state))}</Text>
    </Paper>
  );
}

const ChallengeStateKeys: Record<ChallengeNodeData["state"], TranslationKey> = {
  locked: L.ui.challenge.stateLocked,
  current: L.ui.challenge.stateCurrent,
  completed: L.ui.challenge.stateCompleted,
};

function resolveChallengeStateKey(state: ChallengeNodeData["state"]): TranslationKey {
  return ChallengeStateKeys[state];
}
