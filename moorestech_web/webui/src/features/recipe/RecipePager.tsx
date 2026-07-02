import { ActionIcon, Group, Text } from "@mantine/core";

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
  if (count <= 1) return null;
  return (
    <Group gap="xs">
      <ActionIcon variant="default" size="sm" aria-label="前のレシピ" onClick={() => setIndex((index + count - 1) % count)}>
        &lt;
      </ActionIcon>
      <Text size="sm" c="dimmed">
        {index + 1}/{count}
      </Text>
      <ActionIcon variant="default" size="sm" aria-label="次のレシピ" onClick={() => setIndex((index + 1) % count)}>
        &gt;
      </ActionIcon>
    </Group>
  );
}
