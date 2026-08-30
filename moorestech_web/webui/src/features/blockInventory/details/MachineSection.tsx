import { useState } from "react";
import { Group, Stack } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import LackHighlightText from "./LackHighlightText";
import PowerRateText from "./PowerRateText";
import { machineStateDisplay } from "./detailLogic";
import MachineInventoryBody from "./machine/MachineInventoryBody";
import MachineRecipeSelectionList from "./machine/recipeSelection/MachineRecipeSelectionList";
import SelectedRecipeHeader from "./machine/SelectedRecipeHeader";
import { buildMachineRecipeSelectionRows, hasSelectedRecipe } from "./machine/machineRecipeSelectionLogic";

// 機械: 未選択→レシピ選択モード、選択済→インベントリモード。ヘッダで選択モードへ戻れる（ADR 0042）
// Machine: unselected → recipe-selection mode, selected → inventory mode; the header returns to selection (ADR 0042)
export default function MachineSection({ data, machine }: { data: BlockInventoryOpen; machine: MachineDetailData }) {
  const machineRecipes = useTopic(Topics.machineRecipes);
  // 選択モードを開いた時点のselectedRecipeGuidを覚える。サーバーの応答でこれが実際に変わるまでは
  // 選択モードを閉じない（拒否時にサーバー未反映のままインベントリへ戻ることを防ぐ。C14）
  // Remember selectedRecipeGuid at the moment selection mode was opened; it only closes once the
  // server's response actually changes it (prevents returning to inventory on a rejected change. C14)
  const [openedFromGuid, setOpenedFromGuid] = useState<string | null>(null);
  const { t } = useI18n();

  const rows = buildMachineRecipeSelectionRows(machineRecipes?.recipes ?? [], machine.blockGuid, machine.selectedRecipeGuid);
  const selectedRow = rows.find((row) => row.selected);
  // 状態ラベル+充足率を共通フッタに表示
  // The state label and satisfaction rate stay visible in both modes as the shared footer (ADR 0010)
  const stateDisplay = machineStateDisplay(machine.currentState);
  const footer = (
    <Group justify="center" gap="xs">
      <LackHighlightText insufficient={stateDisplay.insufficient} size="sm" testId="machine-state-label">{t(stateDisplay.labelKey)}</LackHighlightText>
      {stateDisplay.showPowerRate && <PowerRateText currentPower={machine.currentPower} requestPower={machine.requestPower} testId="machine-power-rate" />}
    </Group>
  );

  if (rows.length === 0) {
    return <Stack gap="xs" data-testid="machine-section"><MachineInventoryBody data={data} recipe={null} />{footer}</Stack>;
  }

  const changingRecipe = openedFromGuid !== null && machine.selectedRecipeGuid === openedFromGuid;
  const showSelection = !hasSelectedRecipe(machine.selectedRecipeGuid) || selectedRow === undefined || changingRecipe;
  return (
    <Stack gap="sm" data-testid="machine-section">
      {showSelection ? (
        <MachineRecipeSelectionList rows={rows} />
      ) : (
        <>
          <SelectedRecipeHeader recipe={selectedRow.recipe} onChangeRecipe={() => setOpenedFromGuid(machine.selectedRecipeGuid)} />
          <MachineInventoryBody data={data} recipe={selectedRow.recipe} />
        </>
      )}
      {footer}
    </Stack>
  );
}
