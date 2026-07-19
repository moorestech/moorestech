import type { WebSocketServer } from "ws";
import { Topics, UiStateNames } from "../../src/bridge/transport/protocol";
import type { ClientMsg, ActionPayloads } from "../../src/bridge/transport/protocol";
import type { PlayerInventoryData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { received, state, connections, subscribersOf, topicSubscribers } from "./state";
import { applyMove, applyBlockMove, applyBlockSplit, applyCollect, applyBlockCollect, applyCraft, applySplitDrag } from "./inventoryModel";
import { applyElectricToGearMode, applyFilterMode, applyFilterItem, applyMachineRecipeSelect, applyResearchComplete, applyTrainPlatformMode } from "./detailActions";
import { applySkitAction } from "./skitActions";
import { demoMode, topicData } from "./topics/topicFixtures";
import { knownActions } from "./topics/actionTypes";
// インベントリ状態は接続ごとに分離する。並列テストが同一 inv を奪い合わないため
// Inventory state is isolated per connection so parallel tests don't race on the same inv
export function attachWsHandlers(wss: WebSocketServer) {
  wss.on("connection", (ws) => {
    if (Date.now() < state.rejectConnectionsUntil) {
      ws.close();
      return;
    }
    connections.add(ws);
    let inv: PlayerInventoryData = state.reconnectInventory ?? clone(demoMode ? fx.demoInventory : fx.inventory);
    state.reconnectInventory = null;
    ws.on("close", () => {
      connections.delete(ws);
      if (state.preserveInventoryOnDisconnect) {
        state.reconnectInventory = inv;
        state.preserveInventoryOnDisconnect = false;
      }
      for (const subscribers of topicSubscribers.values()) subscribers.delete(ws);
    });

    ws.on("message", (raw) => {
      const msg = JSON.parse(raw.toString()) as ClientMsg;
      if (msg.op === "ping") {
        send(ws, { op: "pong" });
        return;
      }
      if (msg.op === "subscribe") {
        for (const topic of msg.topics) {
          const subscribers = topicSubscribers.get(topic) ?? new Set();
          subscribers.add(ws);
          topicSubscribers.set(topic, subscribers);
          const data = topicData(topic, inv, demoMode);
          if (data !== undefined) {
            const deliver = () => send(ws, { op: "snapshot", topic, data });
            if (state.snapshotDelayMs > 0 && (state.snapshotDelayTopic === null || state.snapshotDelayTopic === topic)) setTimeout(deliver, state.snapshotDelayMs);
            else deliver();
          }
        }
        return;
      }
      // 購読解除: グローバル購読 Set から除去する（本番 host が unsubscribe を尊重するのに合わせる）
      // Unsubscribe: remove from the global subscription Sets (mirrors the real host honoring unsubscribe)
      if (msg.op === "unsubscribe") {
        for (const topic of msg.topics) {
          topicSubscribers.get(topic)?.delete(ws);
        }
        return;
      }
      if (msg.op === "action") {
        received.push({ type: msg.type, payload: msg.payload });
        // ack は実 host 同様 apply 後に確定し、topic event は数十ms 後に別経路で push（stale grab 再現）
        // ack is decided after apply like the real host; the topic event is pushed later on a separate channel
        let error: string | undefined;
        let skitActionResult: string | null | undefined;
        if (state.injectedActionError?.type === msg.type) {
          error = state.injectedActionError.error;
          state.injectedActionError = null;
        } else if (msg.type === "fail.always") {
          error = "mock_error";
        } else if ((skitActionResult = applySkitAction(msg.type, msg.payload)) !== null) {
          error = skitActionResult ?? undefined;
        } else if (msg.type === "inventory.move_item") {
          // 状態が変化したときだけ topic event を流す（host の失敗は packet を出さない）
          // Emit a topic event only when state changed (the host's failed move sends no packet)
          const moveError = applyMove(inv, msg.payload as ActionPayloads["inventory.move_item"]);
          if (moveError) error = moveError;
          else setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
        } else if (msg.type === "inventory.collect") {
          applyCollect(inv, msg.payload as ActionPayloads["inventory.collect"]);
          setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
        } else if (msg.type === "inventory.split_drag") {
          const splitDragError = applySplitDrag(inv, msg.payload as ActionPayloads["inventory.split_drag"]);
          if (splitDragError) error = splitDragError;
          else setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
        } else if (msg.type === "craft.execute") {
          // 素材が足りる場合のみ消費・生産して inventory topic を再配信する。不足時は実 host のクラフト不成立に相当する no-op（ok:true・トースト回避）
          // Consume+produce and republish the inventory topic only when materials suffice; a shortfall is a no-op (ok:true) mirroring the real host's failed craft
          const guid = (msg.payload as ActionPayloads["craft.execute"]).recipeGuid;
          const recipe = fx.craftRecipes.recipes.find((r) => r.recipeGuid === guid);
          if (recipe && applyCraft(inv, recipe)) {
            setTimeout(() => send(ws, { op: "event", topic: Topics.inventory, data: inv }), 30);
          }
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
            for (const sub of subscribersOf(Topics.modal)) send(sub, { op: "event", topic: Topics.modal, data: { modal: null } });
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
          // 本番と同じ許可表と拒否理由を使う
          // Use the production allowlist and rejection code
          const requestedState = (msg.payload as ActionPayloads["ui_state.request"]).state;
          if (requestedState !== UiStateNames.gameScreen && requestedState !== UiStateNames.playerInventory) {
            error = "transition_not_allowed";
          } else {
            state.currentUiState = { state: requestedState };
            if (requestedState === UiStateNames.gameScreen) state.currentBlock = clone(fx.blockClosed);
            setTimeout(() => {
              for (const sub of subscribersOf(Topics.uiState)) send(sub, { op: "event", topic: Topics.uiState, data: state.currentUiState });
              if (requestedState === UiStateNames.gameScreen) {
                for (const sub of subscribersOf(Topics.blockInventory)) send(sub, { op: "event", topic: Topics.blockInventory, data: state.currentBlock });
              }
            }, 30);
          }
        } else if (msg.type === "filter_splitter.set_mode") {
          const applied = applyFilterMode(state.currentBlock, msg.payload as ActionPayloads["filter_splitter.set_mode"]);
          if (!applied) error = "invalid_direction";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "filter_splitter.set_filter_item") {
          const applied = applyFilterItem(state.currentBlock, msg.payload as ActionPayloads["filter_splitter.set_filter_item"]);
          if (!applied) error = "invalid_slot";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "electric_to_gear.set_output_mode") {
          const applied = applyElectricToGearMode(state.currentBlock, msg.payload as ActionPayloads["electric_to_gear.set_output_mode"]);
          if (!applied) error = "invalid_mode_index";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "train_platform.set_transfer_mode") {
          const applied = applyTrainPlatformMode(state.currentBlock, msg.payload as ActionPayloads["train_platform.set_transfer_mode"]);
          if (!applied) error = "invalid_block_type";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "machine_recipe.select") {
          const applied = applyMachineRecipeSelect(state.currentBlock, msg.payload as ActionPayloads["machine_recipe.select"]);
          if (!applied) error = "invalid_block_type";
          else setTimeout(() => send(ws, { op: "event", topic: Topics.blockInventory, data: state.currentBlock }), 30);
        } else if (msg.type === "research.complete") {
          // 該当ノードを completed 化し、全 research 購読者へ push（研究実行→遷移を再現）
          // Flip the node to completed and push to all research subscribers (reproduces research→transition)
          const applied = applyResearchComplete(state.researchTree, msg.payload as ActionPayloads["research.complete"]);
          if (!applied) error = "research_not_found";
          else setTimeout(() => {
            for (const sub of subscribersOf(Topics.researchTree)) send(sub, { op: "event", topic: Topics.researchTree, data: state.researchTree });
          }, 30);
        } else if (msg.type !== "fail.always" && !knownActions.has(msg.type)) {
          // 未知 action type は本番 dispatcher と同じく unknown_action で拒否する（既知だが未実装の split/sort は no-op で ok:true）
          // Unknown action types are rejected with unknown_action like the real dispatcher (known-but-unimplemented split/sort stay no-op ok:true)
          error = "unknown_action";
        }
        send(ws, { op: "result", requestId: msg.requestId, ok: error === undefined, error });
        return;
      }
    });
  });
}
