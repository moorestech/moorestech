import { useEffect, useMemo, useState } from "react";
import { Button, Group, Title } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { TreeView } from "@/shared/treeView";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { challengeCategoryNameKey, L, useI18n } from "@/shared/i18n";
import ChallengeNodeCard from "./ChallengeNodeCard";
import { findChallengeInitialFocusNode } from "./challengeLogic";
import styles from "./style.module.css";

export default function ChallengePanel() {
  const tree = useTopic(Topics.challengeTree);
  const categories = tree?.categories ?? [];
  const [selectedGuid, setSelectedGuid] = useState<string | null>(null);
  const selected = categories.find((category) => category.guid === selectedGuid) ?? categories[0];
  const { t } = useI18n();
  // 各カテゴリの初回訪問で進行中へ寄せ、再訪は保存視点を戻す
  // Center on in-progress on a category's first visit; revisits restore the stored view
  const initialFocus = useMemo(() => findChallengeInitialFocusNode(selected?.nodes ?? [])?.position ?? null, [selected]);

  useEffect(() => {
    if (selected && selected.guid !== selectedGuid) setSelectedGuid(selected.guid);
  }, [selected, selectedGuid]);

  return (
    <section className={styles.panel} data-testid="challenge-panel" {...tutorialAnchor(TutorialAnchorIds.challengePanel)}>
      <Title order={2}>{t(L.ui.challenge.title)}</Title>
      <Group className={styles.categories} {...tutorialAnchor(TutorialAnchorIds.challengeCategories)}>
        {categories.map((category) => (
          <Button key={category.guid} variant={category.guid === selected?.guid ? "filled" : "subtle"}
            onClick={() => setSelectedGuid(category.guid)} data-testid={`challenge-category-${category.guid}`}>
            {t(challengeCategoryNameKey(category.guid))}
          </Button>
        ))}
      </Group>
      {selected && <TreeView key={selected.guid} viewportKey={`challenge-${selected.guid}`} initialFocus={initialFocus} nodes={selected.nodes} getId={(node) => node.guid} getPosition={(node) => node.position}
        getPrevIds={(node) => node.prevGuids} testIdPrefix="challenge"
        renderNode={(node, point) => <ChallengeNodeCard node={node} left={point.x} top={point.y} />} />}
    </section>
  );
}
