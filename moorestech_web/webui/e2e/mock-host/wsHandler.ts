import type { WebSocketServer } from "ws";
import { Topics, UiStateNames, ACTION_TYPES } from "../../src/bridge/transport/protocol";
import type { ClientMsg, ActionPayloads } from "../../src/bridge/transport/protocol";
import type { PlayerInventoryData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, blockSubscribers, modalSubscribers, uiStateSubscribers, researchTreeSubscribers } from "./state";
import { applyMove, applyBlockMove, applyBlockSplit, applyCollect, applyBlockCollect } from "./inventoryModel";
import { applyFilterMode, applyFilterItem, applyResearchComplete } from "./detailActions";

// 本番 dispatcher が受理する既知 action type。protocol.ts から導出し二重定義を排除する
// Action types the real dispatcher accepts, derived from protocol.ts to kill the duplicate list
const KNOWN_ACTIONS = new Set<string>(ACTION_TYPES);

// インベントリ状態は接続ごとに分離する。並列テストが同一 inv を奪い合わないため
// Inventory state is isolated per connection so parallel tests don't race on the same inv
export function attachWsHandlers(wss: WebSocketServer) {
  wss.on("connection", (ws) => {
    let inv: PlayerInventoryData = clone(fx.inventory);

    const topicData = (topic: string): unknown => {
      if (topic === Topics.inventory) return inv;
      if (topic === Topics.craftRecipes) return fx.craftRecipes;
      if (topic === Topics.machineRecipes) return fx.machineRecipes;
      if (topic === Topics.itemList) return fx.itemList;
      if (topic === Topics.blockInventory) return state.currentBlock;
      if (topic === Topics.modal) return { modal: state.currentModal };
      if (topic === Topics.progress) return fx.progressSample;
      if (topic === Topics.uiState) return state.currentUiState;
      if (topic === Topics.researchTree) return state.researchTree;
      return undefined;
    };

    ws.on("close", () => {
      blockSubscribers.delete(ws);
      modalSubscribers.delete(ws);
      uiStateSubscribers.delete(ws);
      researchTreeSubscribers.delete(ws);
    });

    ws.on("message", (raw) => {
      const msg = JSON.parse(raw.toString()) as ClientMsg;
      if (msg.op === "subscribe") {
        for (const topic of msg.topics) {
          if (topic === Topics.blockInventory) blockSubscribers.add(ws);
          if (topic === Topics.modal) modalSubscribers.add(ws);
          if (topic === Topics.uiState) uiStateSubscribers.add(ws);
          if (topic === Topics.researchTree) researchTreeSubscribers.add(ws);
          const data = topicData(topic);
          if (data !== undefined) send(ws, { op: "snapshot", topic, data });
        }
        return;
      }
      // 購読解除: グローバル購読 Set から除去する（本番 host が unsubscribe を尊重するのに合わせる）
      // Unsubscribe: remove from the global subscription Sets (mirrors the real host honoring unsubscribe)
      if (msg.op === "unsubscribe") {
        for (const topic of msg.topics) {
          if (topic === Topics.blockInventory) blockSubscribers.delete(ws);
          if (topic === Topics.modal) modalSubscribers.delete(ws);
          if (topic === Topics.uiState) uiStateSubscribers.delete(ws);
          if (topic === Topics.researchTree) researchTreeSubscribers.delete(ws);
        }
        return;
      }
      if (msg.op === "action") {
        received.push({ type: msg.type, payload: msg.payload });
        // ack は実 host 同様 apply 後に確定し、topic event は数十ms 後に別経路で push（stale grab 再現）
        // ack is decided after apply like the real host; the topic event is pushed later on a separate channel
        let error: string | undefined;
        if (msg.type === "fail.always") {
          error = "mock_error";
        } else if (msg.type === "inventory.move_item") {
          // 状態が変化したときだけ topic event を流す（host の失敗は packet を出さない）
          // Emit a topic event only when state changed (the host's failed move sends no packet)
          const moveError = applyMove(inv, msg.payload as ActionPayloads["inventory.move_item"]);
          if (moveError) error = moveError;
          else setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
        } else if (msg.type === "inventory.collect") {
          applyCollect(inv, msg.payload as ActionPayloads["inventory.collect"]);
          setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
        } else if (msg.type === "inventory.select_hotbar") {
          // 選択 index を更新して inventory topic を再配信
          // Update the selected index and republish the inventory topic
          const index = (msg.payload as ActionPayloads["inventory.select_hotbar"]).index;
          if (typeof index === "number" && index >= 0 && index < inv.hotbarSlots.length) {
            inv.selectedHotbar = index;
            setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
          } else {
            error = "invalid_index";
          }
        } else if (msg.type === "ui.modal.respond") {
          // どの結果でもモーダルを閉じ、全 modal 購読者へ modal:null を push
          // Any result closes the modal and pushes modal:null to all modal subscribers
          state.currentModal = null;
          setTimeout(() => {
            for (const sub of modalSubscribers) send(sub, { op: "event", topic: Topics.modal, data: { modal: null } });
          }, 30);
        } else if (msg.type === "block_inventory.move_item") {
          const moveError = applyBlockMove(inv, state.currentBlock, msg.payload as ActionPayloads["block_inventory.move_item"]);
          if (moveError) {
            error = moveError;
          } else {
            setTimeout(() => {
              send(ws, { op: "event", topic: Topics.inventory, data: inv });
              send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
            }, 30);
          }
        } else if (msg.type === "block_inventory.split") {
          // 半分計算は host 側の責務。適用成功時のみ両 topic を再配信する
          // The host owns the half computation; republish both topics only when the apply succeeds
          const splitError = applyBlockSplit(inv, state.currentBlock, msg.payload as ActionPayloads["block_inventory.split"]);
          if (splitError) {
            error = splitError;
          } else {
            setTimeout(() => {
              send(ws, { op: "event", topic: Topics.inventory, data: inv });
              send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
            }, 30);
          }
        } else if (msg.type === "block_inventory.collect") {
          // 実 host と同様に集約適用後、inventory と block の両 topic を再配信する
          // Apply the consolidation then republish both the inventory and block topics, like the real host
          applyBlockCollect(inv, state.currentBlock, msg.payload as ActionPayloads["block_inventory.collect"]);
          setTimeout(() => {
            send(ws, { op: "event", topic: Topics.inventory, data: inv });
            send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
          }, 30);
        } else if (msg.type === "ui_state.request") {
          // 実 host の許可制を再現: GameScreen/PlayerInventory のみ受理し、GameScreen 遷移では block も閉じる
          // Mirror the real host's allowlist: accept only GameScreen/PlayerInventory; GameScreen also closes the block
          const requestedState = (msg.payload as ActionPayloads["ui_state.request"]).state;
          if (requestedState !== UiStateNames.gameScreen && requestedState !== UiStateNames.playerInventory) {
            error = "unsupported_state";
          } else {
            state.currentUiState = { state: requestedState };
            if (requestedState === UiStateNames.gameScreen) state.currentBlock = clone(fx.blockClosed);
            setTimeout(() => {
              for (const sub of uiStateSubscribers) send(sub, { op: "event", topic: Topics.uiState, data: state.currentUiState });
              if (requestedState === UiStateNames.gameScreen) {
                for (const sub of blockSubscribers) send(sub, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
              }
            }, 30);
          }
        } else if (msg.type === "filter_splitter.set_mode") {
          // 対象方向の mode を書換え、block topic を再配信する
          // Rewrite the target direction's mode and republish the block topic
          const applied = applyFilterMode(state.currentBlock, msg.payload as ActionPayloads["filter_splitter.set_mode"]);
          if (!applied) error = "invalid_direction";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "filter_splitter.set_filter_item") {
          // filterItemIds[slotIndex] を clear:0/固定grabID へ書換え、block topic を再配信する
          // Rewrite filterItemIds[slotIndex] to clear:0/fixed grab id and republish the block topic
          const applied = applyFilterItem(state.currentBlock, msg.payload as ActionPayloads["filter_splitter.set_filter_item"]);
          if (!applied) error = "invalid_slot";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "research.complete") {
          // 該当ノードを completed 化し、全 research 購読者へ push（研究実行→遷移を再現）
          // Flip the node to completed and push to all research subscribers (reproduces research→transition)
          const applied = applyResearchComplete(state.researchTree, msg.payload as ActionPayloads["research.complete"]);
          if (!applied) error = "research_not_found";
          else setTimeout(() => {
            for (const sub of researchTreeSubscribers) send(sub, { op: "event", topic: Topics.researchTree, data: state.researchTree });
          }, 30);
        } else if (msg.type !== "fail.always" && !KNOWN_ACTIONS.has(msg.type)) {
          // 未知 action type は本番 dispatcher と同じく unknown_action で拒否する（既知だが未実装の split/sort/craft は no-op で ok:true）
          // Unknown action types are rejected with unknown_action like the real dispatcher (known-but-unimplemented split/sort/craft stay no-op ok:true)
          error = "unknown_action";
        }
        send(ws, { op: "result", requestId: msg.requestId, ok: error === undefined, error });
        return;
      }
    });
  });
}
