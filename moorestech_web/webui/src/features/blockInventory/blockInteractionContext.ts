import { createContext, useContext } from "react";
import type { ActionPayloads } from "@/bridge";

type MoveItemPayload = ActionPayloads["block_inventory.move_item"];

// 登録コンポーネントの contract は {data} 固定なので、grab/名前/送信は context で供給する
// The registered component contract is fixed to {data}, so grab/name/dispatch come via context
export type BlockInteraction = {
  grabCount: number;
  resolveName: (itemId: number) => string | undefined;
  dispatch: (payload: MoveItemPayload) => void;
};

const noop: BlockInteraction = {
  grabCount: 0,
  resolveName: () => undefined,
  dispatch: () => undefined,
};

export const BlockInteractionContext = createContext<BlockInteraction>(noop);

export function useBlockInteraction(): BlockInteraction {
  return useContext(BlockInteractionContext);
}
