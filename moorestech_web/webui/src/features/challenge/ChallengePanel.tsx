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
  // 初回表示とカテゴリ切替時に進行中のチャレンジへ寄せる。視点はカテゴリごとに保存する（研究ツリーと同じ）
  // Center on the in-progress challenge on first view and on category switch; the viewport is stored per category (as in the research tree)
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
