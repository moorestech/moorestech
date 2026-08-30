// 機械のレシピ選択モード本体。行クリックで選択Actionを送り、親へ遷移を通知する
// The machine's recipe-selection mode; a row click dispatches the select action and notifies the parent
import { Stack } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { MachineRecipeSelectionRowData } from "../machineRecipeSelectionLogic";
import MachineRecipeSelectionRow from "./MachineRecipeSelectionRow";

type Props = { rows: MachineRecipeSelectionRowData[] };

export default function MachineRecipeSelectionList({ rows }: Props) {
  const onSelect = (recipeGuid: string) => {
    void dispatchAction("machine_recipe.select", { operation: "set", recipeGuid });
  };
  return (
    <Stack gap="xs" data-testid="machine-recipe-selection">
      {rows.map((row) => <MachineRecipeSelectionRow key={row.recipe.recipeGuid} row={row} onSelect={onSelect} />)}
    </Stack>
  );
}
