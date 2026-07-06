import { Group, Text } from "@mantine/core";
import { ItemSlot } from "@/shared/ui";

// 選択アイテムのアイコン+名前ヘッダ
// Icon + name header for the selected item
export default function ItemHeader({ itemId, name }: { itemId: number; name: string }) {
  return (
    <Group gap="xs">
      <ItemSlot itemId={itemId} name={name} />
      <Text>{name}</Text>
    </Group>
  );
}
