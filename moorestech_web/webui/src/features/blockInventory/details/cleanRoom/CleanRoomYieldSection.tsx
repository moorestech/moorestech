import { Stack, Text } from "@mantine/core";
import type { CleanRoomYieldData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import BlockInfoUnavailableLine from "../blockInfoFetch/BlockInfoUnavailableLine";
import { cleanRoomYieldLines } from "./cleanRoomYieldLogic";

// 抽選の中身欄: 選択工程の上限グレード・昇格率・失敗率・損失率を部屋欄の下に並べる
// Draw-detail section: max grade, promotion chance, fail rate and loss rate of the selected process, under the room section
export default function CleanRoomYieldSection({ cleanRoomYield }: { cleanRoomYield: CleanRoomYieldData }) {
  const { t } = useI18n();
  if (cleanRoomYield.fetchState !== "fetched") return <BlockInfoUnavailableLine unavailable={cleanRoomYield} targetKey={L.ui.blockInventory.cleanRoomYieldInfoLabel} testId="clean-room-yield-fetch-state" />;
  return (
    <Stack gap={2} data-testid="clean-room-yield-section">
      {cleanRoomYieldLines(cleanRoomYield).map((line) => (
        <Text key={line.testId} size="xs" c="var(--text-default)" data-testid={line.testId}>
          {t(line.key, line.params)}
        </Text>
      ))}
    </Stack>
  );
}
