import { Text } from "@mantine/core";
import type { BlockInfoUnavailableData } from "@/bridge";
import { useI18n, type TranslationKey } from "@/shared/i18n";
import { blockInfoUnavailableKey } from "./blockInfoFetchStateLogic";

type Props = { unavailable: BlockInfoUnavailableData; targetKey: TranslationKey; testId: string };

// 照会欄が取得できていない間の1行。欄の名前と取得状態だけを出し、前回値は出さない
// One row while a query section is unavailable; shows only the section name and the fetch state, never a previous value
export default function BlockInfoUnavailableLine({ unavailable, targetKey, testId }: Props) {
  const { t } = useI18n();
  return (
    <Text size="xs" c="var(--text-default)" data-testid={testId} data-fetch-state={unavailable.fetchState}>
      {t(blockInfoUnavailableKey(unavailable), { target: t(targetKey) })}
    </Text>
  );
}
