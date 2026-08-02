#!/usr/bin/env node
// AskUserQuestionの完了＝裁定の確定。意思決定台帳への記録をリマインドする
// A completed AskUserQuestion is a user ruling; remind the agent to record it

const message =
  "ユーザーの裁定が確定した。棄却された生きた代替案があるなら、今すぐ .decisions/YYYY-MM-DD-<内容>.md へ「決定/棄却案/理由/リンク」を数行で記録すること。生きた代替案の無い一本道の選択なら記録不要。";

console.log(
  JSON.stringify({
    hookSpecificOutput: {
      hookEventName: "PostToolUse",
      additionalContext: message,
    },
  })
);
