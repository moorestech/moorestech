import { Group } from "@mantine/core";
import type { FluidSlotData } from "@/bridge/contract/payloadTypes";
import FluidSlot from "../FluidSlot";
import ProgressArrow from "../ProgressArrow";

type Props = {
  fluids: FluidSlotData[];
  // 進捗矢印は progress が数値のときだけ描画する（null/undefined は非表示で統一）
  // Draw the progress arrow only for a numeric progress (null/undefined uniformly hides it)
  progress?: number | null;
  testId: string;
};

// 流体スロット横並び＋任意の進捗矢印。Tank/Generic/Machine の3重複を置き換える共通部品
// Fluid slots in a row plus an optional progress arrow; replaces the Tank/Generic/Machine triplication
export default function FluidSlotRow({ fluids, progress, testId }: Props) {
  if (fluids.length === 0) return null;
  return (
    <Group data-testid={testId} gap="xs" align="center">
      {fluids.map((fluid, i) => (
        <FluidSlot key={i} fluid={fluid} />
      ))}
      {progress != null ? <ProgressArrow value={progress} /> : null}
    </Group>
  );
}
