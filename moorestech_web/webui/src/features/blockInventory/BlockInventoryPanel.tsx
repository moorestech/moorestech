import { useTopic, dispatchAction, Topics } from "@/bridge";
import type { ActionPayloads } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { resolveBlockComponent } from "./blockLogic";
import { BlockInteractionContext, type BlockInteraction } from "./blockInteractionContext";

// ブロック UI のオーバーレイ。uGUI の SubInventoryState 相当で、blockType から中身を静的解決する
// Block UI overlay; the SubInventoryState equivalent, statically resolving the body from blockType
export default function BlockInventoryPanel() {
  const data = useTopic(Topics.blockInventory);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  // 未受信または閉状態なら何も描画しない（ブロック UI が開いていない）
  // Render nothing when not received or closed (no block UI is open)
  if (!data || !data.open) return null;

  // grab/名前/送信を context へまとめ、{data} 固定 contract の登録コンポーネントへ供給する
  // Bundle grab/name/dispatch into context and supply them to registered {data}-contract components
  const interaction: BlockInteraction = {
    grabCount: inventory?.grab.count ?? 0,
    resolveName: (itemId) => itemMaster?.get(itemId)?.name,
    dispatch: (payload: ActionPayloads["block_inventory.move_item"]) =>
      void dispatchAction("block_inventory.move_item", payload),
  };

  // blockType に対応するコンポーネントを解決（未登録は GenericBlockInventory にフォールバック）
  // Resolve the component for blockType (unregistered falls back to GenericBlockInventory)
  const Body = resolveBlockComponent(data.blockType);

  // z-30: grab オーバーレイ(z-40)とトースト(z-50)の下に重なる固定中央パネル
  // z-30: a fixed centered panel sitting under the grab overlay (z-40) and toasts (z-50)
  return (
    <BlockInteractionContext.Provider value={interaction}>
      <div
        data-testid="block-inventory"
        className="fixed left-1/2 top-24 -translate-x-1/2 z-30 bg-gray-800 border border-gray-700 rounded p-4 text-gray-300"
      >
        <h2 className="text-lg font-semibold mb-3">{data.blockName}</h2>
        <Body data={data} />
      </div>
    </BlockInteractionContext.Provider>
  );
}
