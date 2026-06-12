import ItemSlot from "./ItemSlot";

// 選択アイテムのアイコン+名前ヘッダ
// Icon + name header for the selected item
export default function ItemHeader({ itemId, name }: { itemId: number; name: string }) {
  return (
    <div className="flex items-center gap-2">
      <ItemSlot itemId={itemId} name={name} />
      <span className="text-base text-gray-200">{name}</span>
    </div>
  );
}
