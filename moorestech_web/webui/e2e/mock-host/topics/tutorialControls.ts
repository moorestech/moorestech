import { Topics, type TopicPayloads } from "../../../src/bridge/transport/protocol";
import { recipeItemAnchorId, researchNodeAnchorId, TutorialAnchorIds } from "../../../src/shared/tutorialAnchor/anchorIds";
import * as fx from "../fixtures";

// 研究ノード枠の余白。spec と共有する正本
// Padding of the research node outline; the single value shared with the spec
export const TUTORIAL_RESEARCH_NODE_PADDING_PX = 8;

type TutorialData = TopicPayloads[typeof Topics.tutorialPresentation];
const tutorial = (data: TutorialData) => ({ kind: "set" as const, topic: Topics.tutorialPresentation, data });

// 表示制御の展開元
// Source of presentation controls
export const tutorialControls = {
  tutorialOutline: () => tutorial({
    revision: 1,
    sessions: [{
      tutorialSessionId: "tutorial-session-1", challengeId: "tutorial-challenge-1",
      elements: [{
        kind: "outline" as const,
        elementId: "tutorial-highlight-1",
        anchorId: "game.crosshair",
        paddingPx: 8, blocksPointerInput: false,
      }],
    }],
  }),
  // パンでクリップ境界を跨ぐノード
  // A node that crosses the clip edge on pan
  tutorialResearchNode: () => tutorial({
    revision: 1,
    sessions: [{
      tutorialSessionId: "tutorial-session-research", challengeId: "tutorial-challenge-research",
      elements: [{
        kind: "outline" as const,
        elementId: "tutorial-highlight-research",
        anchorId: researchNodeAnchorId(fx.researchableNodeGuid),
        paddingPx: TUTORIAL_RESEARCH_NODE_PADDING_PX, blocksPointerInput: false,
      }],
    }],
  }),
  // アイテム一覧の1段目セルをラベル付きで指す。ScrollAreaのクリップでラベルが落ちないことの検証用
  // Points at a first-row item-list cell with a label, to verify the ScrollArea clip does not drop it
  tutorialRecipeItem: () => tutorial({
    revision: 4,
    sessions: [{
      tutorialSessionId: "tutorial-session-recipe-item", challengeId: "tutorial-challenge-recipe-item",
      elements: [{
        kind: "outline" as const, elementId: "tutorial-highlight-recipe-item",
        anchorId: recipeItemAnchorId(fx.TUTORIAL_RECIPE_ITEM_ID),
        paddingPx: 8, blocksPointerInput: false, labelTutorialGuid: fx.OUTLINE_LABEL_TUTORIAL_GUID,
      }],
    }],
  }),
  tutorialEmpty: () => tutorial({ revision: 0, sessions: [] }),
  tutorialOutlineWithLabel: () => tutorial({
    revision: 2,
    sessions: [{
      tutorialSessionId: "tutorial-session-1", challengeId: "tutorial-challenge-1",
      elements: [{
        kind: "outline" as const, elementId: "tutorial-highlight-2", anchorId: "game.crosshair",
        paddingPx: 8, blocksPointerInput: false, labelTutorialGuid: fx.OUTLINE_LABEL_TUTORIAL_GUID,
      }],
    }],
  }),
  tutorialKeyControl: () => tutorial({
    revision: 3,
    sessions: [{
      tutorialSessionId: "tutorial-session-1", challengeId: "tutorial-challenge-1",
      elements: [{
        kind: "keyControl" as const, elementId: "tutorial-key-1",
        tutorialGuid: fx.KEY_CONTROL_TUTORIAL_GUID, keyName: "Tab", uiState: "GameScreen",
      }],
    }],
  }),
  // レシピ一覧のセルからホットバーへのドラッグ誘導。参照キャプチャの動画で使う
  // Drag guide from a recipe list cell to the hotbar, used by the reference capture video
  tutorialDragGuide: () => tutorial({
    revision: 5,
    sessions: [{
      tutorialSessionId: "tutorial-session-drag-guide", challengeId: "tutorial-challenge-drag-guide",
      elements: [{
        kind: "dragGuide" as const, elementId: "tutorial-drag-guide-1",
        fromAnchorId: recipeItemAnchorId(fx.TUTORIAL_RECIPE_ITEM_ID), toAnchorId: TutorialAnchorIds.hotbarHud,
      }],
    }],
  }),
};
