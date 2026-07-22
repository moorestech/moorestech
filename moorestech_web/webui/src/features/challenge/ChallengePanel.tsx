import { useEffect, useState } from "react";
import { Button, Group, Title } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { TreeView } from "@/shared/treeView";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { useI18n } from "@/shared/i18n";
import ChallengeNodeCard from "./ChallengeNodeCard";
import styles from "./style.module.css";

export default function ChallengePanel() {
  const tree = useTopic(Topics.challengeTree);
  const categories = tree?.categories ?? [];
  const [selectedGuid, setSelectedGuid] = useState<string | null>(null);
  const selected = categories.find((category) => category.guid === selectedGuid) ?? categories[0];
  const { t } = useI18n();

  useEffect(() => {
    if (selected && selected.guid !== selectedGuid) setSelectedGuid(selected.guid);
  }, [selected, selectedGuid]);

  return (
    <section className={styles.panel} data-testid="challenge-panel" {...tutorialAnchor(TutorialAnchorIds.challengePanel)}>
      <Title order={2}>{t("challenge.title")}</Title>
      <Group className={styles.categories} {...tutorialAnchor(TutorialAnchorIds.challengeCategories)}>
        {categories.map((category) => (
          <Button key={category.guid} variant={category.guid === selected?.guid ? "filled" : "subtle"}
            onClick={() => setSelectedGuid(category.guid)} data-testid={`challenge-category-${category.guid}`}>
            {category.name}
          </Button>
        ))}
      </Group>
      {selected && <TreeView nodes={selected.nodes} getId={(node) => node.guid} getPosition={(node) => node.position}
        getPrevIds={(node) => node.prevGuids} nodeTargetSelector="[data-challenge-node]" testIdPrefix="challenge"
        renderNode={(node, point) => <ChallengeNodeCard node={node} left={point.x} top={point.y} />} />}
    </section>
  );
}
