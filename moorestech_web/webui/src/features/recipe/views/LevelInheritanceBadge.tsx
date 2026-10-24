import { Text } from "@mantine/core";
import type { MachineRecipeLevelInheritance } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { levelInheritanceBadgeText } from "../logic/levelInheritanceBadge";

type Props = { inheritance: MachineRecipeLevelInheritance; testId: string };

// レベル継承レシピのバッジ。レシピ選択行とレシピビューアが共有する
// Level-inheritance badge shared by the recipe selection row and the recipe viewer
export default function LevelInheritanceBadge({ inheritance, testId }: Props) {
  const { t } = useI18n();
  const badge = levelInheritanceBadgeText(inheritance);
  if (badge === null) return null;
  return (
    <Text size="xs" c="dimmed" data-testid={testId}>
      {t(badge.key, badge.values)}
    </Text>
  );
}
