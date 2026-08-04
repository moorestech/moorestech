import { ActionIcon, Group, Text } from "@mantine/core";
import { L, useI18n } from "@/shared/i18n";

// 複数レシピの前後送りページャ（< i/n >）
// Pager for stepping through multiple recipes (< i/n >)
export default function RecipePager({
  index,
  count,
  setIndex,
}: {
  index: number;
  count: number;
  setIndex: (i: number) => void;
}) {
  const { t } = useI18n();
  if (count <= 1) return null;
  return (
    <Group gap="xs">
      <ActionIcon variant="default" size="sm" aria-label={t(L.ui.recipe.previousRecipe)} onClick={() => setIndex((index + count - 1) % count)}>
        {t(L.ui.recipe.previousSymbol)}
      </ActionIcon>
      <Text size="sm" c="dimmed">
        {t(L.ui.recipe.pageIndicator, { current: index + 1, count })}
      </Text>
      <ActionIcon variant="default" size="sm" aria-label={t(L.ui.recipe.nextRecipe)} onClick={() => setIndex((index + 1) % count)}>
        {t(L.ui.recipe.nextSymbol)}
      </ActionIcon>
    </Group>
  );
}
