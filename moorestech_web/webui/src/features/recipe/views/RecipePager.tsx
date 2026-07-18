import { ActionIcon, Group, Text } from "@mantine/core";
import { useI18n } from "@/shared/i18n";

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
      <ActionIcon variant="default" size="sm" aria-label={t("前のレシピ")} onClick={() => setIndex((index + count - 1) % count)}>
        {t("<")}
      </ActionIcon>
      <Text size="sm" c="dimmed">
        {t("{current}/{count}", { current: index + 1, count })}
      </Text>
      <ActionIcon variant="default" size="sm" aria-label={t("次のレシピ")} onClick={() => setIndex((index + 1) % count)}>
        {t(">")}
      </ActionIcon>
    </Group>
  );
}
