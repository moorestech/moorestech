import { Topics } from "../../src/bridge/transport/protocol";
import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import type { SkitPresentationData } from "../../src/bridge/contract/payloadTypes";
import * as fx from "./fixtures";
import { send, clone } from "./wire";
import { state, subscribersOf } from "./state";

const SKIT_ACTIONS = new Set([
  "skit.advance", "skit.select", "skit.set_auto", "skit.skip", "skit.set_ui_hidden",
]);

export function applySkitAction(type: string, payload: unknown): string | null | undefined {
  if (!SKIT_ACTIONS.has(type)) return null;
  const base = payload as ActionPayloads["skit.advance"];
  const staleError = validateBase(base);
  if (staleError) return staleError;

  if (type === "skit.advance") return applyAdvance();
  if (type === "skit.select") return applySelect(payload as ActionPayloads["skit.select"]);
  if (type === "skit.set_auto") return applyControls({ autoEnabled: (payload as ActionPayloads["skit.set_auto"]).enabled });
  if (type === "skit.skip") return applyControls({ skipActive: true });
  return applyControls({ uiHidden: (payload as ActionPayloads["skit.set_ui_hidden"]).hidden });
}

function validateBase(payload: ActionPayloads["skit.advance"]): string | undefined {
  if (payload.sessionId !== state.skitPresentation.sessionId) return "stale_session";
  if (payload.sceneRevision !== state.skitPresentation.sceneRevision) return "stale_revision";
  return undefined;
}

function applyAdvance(): string | undefined {
  if (!state.skitPresentation.allowedIntents.includes("advance")) return "intent_not_allowed";
  state.skitPresentation = {
    ...clone(fx.blockingSkitChoices),
    sessionId: state.skitPresentation.sessionId,
    sceneRevision: state.skitPresentation.sceneRevision + 1,
  };
  publish();
  return undefined;
}

function applySelect(payload: ActionPayloads["skit.select"]): string | undefined {
  if (!state.skitPresentation.allowedIntents.includes("select")) return "intent_not_allowed";
  const known = state.skitPresentation.presentationState.choices.some((choice) => choice.choiceId === payload.choiceId);
  if (!known) return "unknown_choice";
  state.skitPresentation = {
    ...clone(fx.skitPresentation), sessionId: payload.sessionId,
    sceneRevision: state.skitPresentation.sceneRevision + 1,
  };
  publish();
  return undefined;
}

function applyControls(update: Partial<SkitPresentationData["presentationState"]>): string | undefined {
  const intent = "autoEnabled" in update ? "set-auto" : "skipActive" in update ? "skip" : "set-ui-hidden";
  if (!state.skitPresentation.allowedIntents.includes(intent)) return "intent_not_allowed";
  state.skitPresentation = {
    ...state.skitPresentation,
    sceneRevision: state.skitPresentation.sceneRevision + 1,
    presentationState: { ...state.skitPresentation.presentationState, ...update },
  };
  publish();
  return undefined;
}

function publish() {
  for (const ws of subscribersOf(Topics.skitPresentation)) {
    send(ws, { op: "event", topic: Topics.skitPresentation, data: state.skitPresentation });
  }
}
