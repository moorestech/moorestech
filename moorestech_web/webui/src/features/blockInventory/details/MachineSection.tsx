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
  // 選択モードで最後に要求したレシピGUIDを覚える。サーバーの選択がこれと一致した時点で閉じるので、
  // 同一レシピを選び直しても閉じられ、拒否された間は選択モードに留まる（C14）
  // Remember the recipe GUID last requested in selection mode; the mode closes once the server's selection
  // matches it, so re-picking the same recipe still closes and a rejection keeps the mode open (C14)
  const [selectionOpened, setSelectionOpened] = useState(false);
  const [requestedRecipeGuid, setRequestedRecipeGuid] = useState<string | null>(null);
  const openSelection = () => {
    setSelectionOpened(true);
    setRequestedRecipeGuid(null);
  };
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
    return <Stack gap="xs" data-testid="machine-section"><MachineInventoryBody data={data} />{footer}</Stack>;
  }

  // 要求したレシピがサーバーの選択に一致するまで選択モードを閉じない（未要求＝開いた直後は留まる）
  // Selection mode stays open until the requested recipe matches the server's selection (no request yet = keep waiting)
  const requestApplied = requestedRecipeGuid !== null && machine.selectedRecipeGuid === requestedRecipeGuid;
  const inSelectionMode = selectionOpened && !requestApplied;
  const showSelection = !hasSelectedRecipe(machine.selectedRecipeGuid) || selectedRow === undefined || inSelectionMode;
  return (
    <Stack gap="sm" data-testid="machine-section">
      {showSelection ? (
        <MachineRecipeSelectionList rows={rows} onSelected={setRequestedRecipeGuid} />
      ) : (
        <>
          <SelectedRecipeHeader recipe={selectedRow.recipe} subject={selectedRow.subject} onChangeRecipe={openSelection} />
          <MachineInventoryBody data={data} />
        </>
      )}
      {footer}
    </Stack>
  );
}
