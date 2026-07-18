import { Text } from "@mantine/core";
import type { TextProps } from "@mantine/core";
import { useI18n } from "@/shared/i18n";

type Props = Omit<TextProps, "children">;

// 初回データ待機中の共通表示
// Shared placeholder shown while initial data is pending
export default function ConnectingPlaceholder(props: Props) {
  const { t } = useI18n();
  return <Text size="sm" c="dimmed" {...props}>{t("connecting...")}</Text>;
}
