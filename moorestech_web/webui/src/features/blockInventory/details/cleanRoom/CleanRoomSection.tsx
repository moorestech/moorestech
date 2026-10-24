import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import LackHighlightText from "../LackHighlightText";
import { cleanRoomDisplay } from "./cleanRoomLogic";

// 部屋欄: クラス（または密閉できていない理由）・空気・清浄機・機械の停止理由を最上部に並べる
// Room section: class (or why it is not sealed), air, purifiers and the machine's halt reason at the top
export default function CleanRoomSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.cleanRoom) return null;
  const view = cleanRoomDisplay(data.cleanRoom);
  return (
    <Stack gap={2} data-testid="clean-room-section">
      <LackHighlightText size="sm" insufficient={view.headlineInsufficient} testId="clean-room-headline">
        {t(view.headline.key, view.headline.params)}
      </LackHighlightText>
      {view.air ? (
        <Text size="sm" c="var(--text-default)" data-testid="clean-room-air">
          {t(view.air.key, view.air.params)}
        </Text>
      ) : null}
      {view.filters.map((filter, index) => (
        <Text key={index} size="xs" c="var(--text-default)" data-testid={`clean-room-air-filter-${index}`}>
          {t(filter.key, filter.params)}
        </Text>
      ))}
      {view.noFilter ? (
        <LackHighlightText size="xs" insufficient testId="clean-room-no-filter">
          {t(view.noFilter.key)}
        </LackHighlightText>
      ) : null}
      {view.haltReason ? (
        <LackHighlightText size="sm" insufficient testId="clean-room-halt-reason">
          {t(view.haltReason.key)}
        </LackHighlightText>
      ) : null}
    </Stack>
  );
}
