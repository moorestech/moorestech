import { Text } from "@mantine/core";
import type { TextProps } from "@mantine/core";

type Props = Omit<TextProps, "children">;

// 初回データ待機中の共通表示
// Shared placeholder shown while initial data is pending
export default function ConnectingPlaceholder(props: Props) {
  return <Text size="sm" c="dimmed" {...props}>connecting...</Text>;
}
