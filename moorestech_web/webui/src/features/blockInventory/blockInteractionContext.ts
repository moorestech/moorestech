import { createContext, useContext } from "react";

// 登録コンポーネントの contract は {data} 固定なので、grab と名前解決を context で供給する
// The registered component contract is fixed to {data}, so grab and name resolution come via context
// 送信は dispatchAction のモジュール直 import に移し、context 値を grab/名前のみに縮めて memo 安定化する
// Dispatch moved to a direct dispatchAction module import; the context value is trimmed to grab/name so it memoizes stably
export type BlockInteraction = {
  grabCount: number;
  resolveName: (itemId: number) => string | undefined;
};

const noop: BlockInteraction = {
  grabCount: 0,
  resolveName: () => undefined,
};

export const BlockInteractionContext = createContext<BlockInteraction>(noop);

export function useBlockInteraction(): BlockInteraction {
  return useContext(BlockInteractionContext);
}
