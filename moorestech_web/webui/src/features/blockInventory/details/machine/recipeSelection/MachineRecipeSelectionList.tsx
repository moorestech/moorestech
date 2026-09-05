// 機械のレシピ選択モード本体。行クリックで選択Actionを送り、親へ遷移を通知する
// The machine's recipe-selection mode; a row click dispatches the select action and notifies the parent
import { dispatchAction } from "@/bridge";
import { RecipeListScrollArea } from "@/shared/ui";
import type { MachineRecipeSelectionRowData } from "../machineRecipeSelectionLogic";
import MachineRecipeSelectionRow from "./MachineRecipeSelectionRow";
import styles from "./machineRecipeSelectionList.module.css";

type Props = { rows: MachineRecipeSelectionRowData[]; onSelected: (recipeGuid: string) => void };

export default function MachineRecipeSelectionList({ rows, onSelected }: Props) {
  const selectedRecipeGuid = rows.find((row) => row.selected)?.recipe.recipeGuid;
  // 選択中の行はサーバー状態が既に一致しているのでActionを送らず閉じるだけにする。
  // 送った場合は送信したGUIDを親へ伝え、サーバーの選択が追いついた時点で閉じさせる
  // The already-selected row needs no action since the server state matches; it just closes.
  // Otherwise the dispatched GUID goes to the parent, which closes once the server catches up
  const onSelect = (recipeGuid: string) => {
    if (recipeGuid !== selectedRecipeGuid) void dispatchAction("machine_recipe.select", { operation: "set", recipeGuid });
    onSelected(recipeGuid);
  };
  // 高さはパネル本文が決める
  // The panel body sets the height
  return (
    <RecipeListScrollArea scrollClassName={null} listClassName={styles.list} listGap="var(--machine-recipe-row-gap)" listTestId="machine-recipe-selection">
      {rows.map((row) => <MachineRecipeSelectionRow key={row.recipe.recipeGuid} row={row} onSelect={onSelect} />)}
    </RecipeListScrollArea>
  );
}
