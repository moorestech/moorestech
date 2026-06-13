import { useTopic } from "../bridge/useTopic";
import { useItemMaster } from "../bridge/useItemMaster";
import ItemSlot from "./ItemSlot";

type Props = {
  selectedItemId: number | null;
  onSelect: (itemId: number) => void;
};

// 右カラム: 表示対象アイテムの一覧（uGUI の ItemListView 準拠）。クリックで中央にレシピ表示
// Right column: list of viewable items, like uGUI's ItemListView; click shows recipes in the center
export default function ItemListPanel({ selectedItemId, onSelect }: Props) {
  const itemList = useTopic("recipe_viewer.item_list");
  const itemMaster = useItemMaster();

  return (
    <div className="space-y-3 [grid-area:items]">
      <h2 className="text-lg font-semibold">Items</h2>
      {itemList ? (
        <div className="grid grid-cols-5 gap-1 w-fit max-h-[70vh] overflow-y-auto pr-1">
          {itemList.itemIds.map((id) => (
            <ItemSlot
              key={id}
              itemId={id}
              name={itemMaster?.get(id)?.name}
              selected={id === selectedItemId}
              onLeftDown={() => onSelect(id)}
            />
          ))}
        </div>
      ) : (
        <div className="text-sm text-gray-400">connecting...</div>
      )}
    </div>
  );
}
