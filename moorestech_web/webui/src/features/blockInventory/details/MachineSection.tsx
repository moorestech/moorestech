import { useState } from "react";
import { Group, Stack } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";
import MachineStateRow from "./rows/MachineStateRow";
import styles from "../style.module.css";
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
  const rows = buildMachineRecipeSelectionRows(machineRecipes?.recipes ?? [], machine.blockGuid, machine.selectedRecipeGuid);
  const selectedRow = rows.find((row) => row.selected);
  // 状態ラベル+充足率を共通フッタに表示
  // The state label and satisfaction rate stay visible in both modes as the shared footer (ADR 0010)
  const footer = (
    <Group justify="center" gap="xs">
      <MachineStateRow currentState={machine.currentState} currentPower={machine.currentPower} requestPower={machine.requestPower} stateTestId="machine-state-label" powerRateTestId="machine-power-rate" />
    </Group>
  );

  // 要求したレシピがサーバーの選択に一致するまで選択モードを閉じない（未要求＝開いた直後は留まる）
  // Selection mode stays open until the requested recipe matches the server's selection (no request yet = keep waiting)
  const requestApplied = requestedRecipeGuid !== null && machine.selectedRecipeGuid === requestedRecipeGuid;
  const inSelectionMode = selectionOpened && !requestApplied;
  // 選べるレシピが無い機械は選択モードを持たず、常にインベントリモードで開く
  // A machine with no selectable recipe has no selection mode and always opens in inventory mode
  const showSelection = rows.length > 0
    && (!hasSelectedRecipe(machine.selectedRecipeGuid) || selectedRow === undefined || inSelectionMode);

  // 外殻がフッタと伸長を1箇所で持ち、モードで分かれるのは中身だけ。これで両モードのフッタ位置が揃う
  // One shell owns the footer and the stretch while only the body branches, keeping the footer at the same height in both modes
  return (
    <Stack className={styles.fillPanelHeight} gap="sm" data-testid="machine-section">
      {showSelection ? (
        <MachineRecipeSelectionList rows={rows} onSelected={setRequestedRecipeGuid} />
      ) : (
        <Stack className={styles.fillPanelHeight} gap="sm">
          {selectedRow === undefined ? null : <SelectedRecipeHeader recipe={selectedRow.recipe} subject={selectedRow.subject} onChangeRecipe={openSelection} />}
          <MachineInventoryBody data={data} />
        </Stack>
      )}
      {footer}
    </Stack>
  );
}
