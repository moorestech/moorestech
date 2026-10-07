#!/usr/bin/env node
// repoのスキルと個人スキルの名前衝突をセッション開始時に検知して警告する
// Detect name collisions between repo skills and personal skills at session start and warn.
//
// Claude Code は同名なら個人版（~/.claude/skills）を優先するため、同名の repo スキルは黙って読まれない。
// 個人スキルは repo 外にあり CI から見えないので、各マシンのセッション開始時にここで突き合わせる。
// 登録: Claude SessionStart（引数 claude → systemMessage で人にも見せる）・Codex SessionStart（引数 codex → 平文）。
// 裁定: .decisions/2026-10-06-repoスキルは個人版と同名にしない.md
// Claude Code prefers the personal copy (~/.claude/skills) on a name clash, so a same-named repo skill is silently ignored.
// Personal skills live outside the repo and are invisible to CI, so each machine checks them here at session start.
// Wired to Claude SessionStart (arg "claude" -> systemMessage shown to the human) and Codex SessionStart (arg "codex" -> plain text).
// Ruling: .decisions/2026-10-06-repoスキルは個人版と同名にしない.md

import { existsSync, readdirSync, readFileSync, realpathSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

// uloop CLI が `uloop skills install` で repo と個人の両方へ配る配布物は、削除しても再生成されるため対象外
// Skills distributed by `uloop skills install` into both places come back when deleted, so they are exempt.
const exemptPrefixes = ["uloop-"];

const runtime = process.argv[2] === "codex" ? "codex" : "claude";
const repoRoot = resolveRepoRoot(readHookInput());
const repoSkillsDir = join(repoRoot, ".agents", "skills");
const personalSkillsDirs = uniqueRealDirs([
  join(homedir(), ".claude", "skills"),
  join(homedir(), ".codex", "skills"),
  join(homedir(), ".agents", "skills"),
]);

// 名前ごとの所在を集め、repo側と個人側の両方にある名前を衝突とする
// Gather locations per name and treat names present on both sides as collisions.
const repoSkills = collectSkillNames(repoSkillsDir);
const personalSkills = new Map();
for (const dir of personalSkillsDirs) {
  for (const [name, path] of collectSkillNames(dir)) {
    if (!personalSkills.has(name)) personalSkills.set(name, path);
  }
}
const collisions = [...repoSkills.keys()].filter((name) => personalSkills.has(name)).sort();
const exempted = collisions.filter((name) => exemptPrefixes.some((p) => name.startsWith(p)));
const reported = collisions.filter((name) => !exempted.includes(name));

// 対象外にした衝突も黙らせず、理由を開発者向けに標準エラーへ残す
// Exempted collisions are not silenced; log them with the reason to stderr for developers.
if (exempted.length > 0) {
  console.error(
    `[skill-name-collision-check] uloop配布スキルの同名は対象外として無視: ${exempted.join(", ")}（uloop skills install が両側を再生成するため）`
  );
}
if (reported.length === 0) process.exit(0);

emitWarning(reported);
process.exit(0);

// 警告文を組み立て、ランタイムに合わせた形式で出力する
// Build the warning and print it in the shape each runtime expects.
function emitWarning(names) {
  const lines = names.map(
    (name) => `- ${name}: repo ${repoSkills.get(name)} ／ 個人 ${personalSkills.get(name)}`
  );
  const message = [
    `⚠ スキル名の衝突 ${names.length}件: repoの .agents/skills と個人スキルに同じ名前がある。Claude Code は個人版を優先するため、repo版（frontmatter hooks を含む）は読まれない。`,
    ...lines,
    "対処: moorestech固有なら repo 側を moores- 接頭辞へ改名して呼び出し元を更新し、個人版とほぼ同じ写しなら repo から削除する（.decisions/2026-10-06-repoスキルは個人版と同名にしない.md）。",
  ].join("\n");
  if (runtime === "codex") {
    console.log(message);
    return;
  }
  console.log(
    JSON.stringify({
      systemMessage: message,
      hookSpecificOutput: { hookEventName: "SessionStart", additionalContext: message },
    })
  );
}

// ディレクトリ直下のスキルを「名前→パス」で集める（ディレクトリ名と frontmatter の name の両方を名前とみなす）
// Collect skills directly under a directory as name->path, counting both the folder name and the frontmatter name.
function collectSkillNames(dir) {
  const names = new Map();
  if (!existsSync(dir)) return names;
  for (const entry of readdirSync(dir)) {
    const skillFile = join(dir, entry, "SKILL.md");
    if (!existsSync(skillFile)) continue;
    const skillDir = join(dir, entry);
    names.set(entry, skillDir);
    const frontmatterName = readFrontmatterName(skillFile);
    if (frontmatterName) names.set(frontmatterName, skillDir);
  }
  return names;
}

// SKILL.md の frontmatter から name を取り出す（無ければ null）
// Read the name field from the SKILL.md frontmatter (null when absent).
function readFrontmatterName(skillFile) {
  const text = readFileSync(skillFile, "utf8");
  const frontmatter = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!frontmatter) return null;
  const name = frontmatter[1].match(/^name:\s*["']?([^"'\r\n]+?)["']?\s*$/m);
  return name ? name[1] : null;
}

// symlink を実体へ解決して同じディレクトリを二重に数えない
// Resolve symlinks so the same directory is not counted twice.
function uniqueRealDirs(dirs) {
  const seen = new Set();
  return dirs.filter((dir) => {
    if (!existsSync(dir)) return false;
    const real = realpathSync(dir);
    if (seen.has(real)) return false;
    seen.add(real);
    return true;
  });
}

// フック入力の cwd → CLAUDE_PROJECT_DIR → スクリプト位置の順で repo ルートを決める
// Resolve the repo root from hook cwd, then CLAUDE_PROJECT_DIR, then this script's location.
function resolveRepoRoot(hookInput) {
  const scriptRoot = dirname(dirname(fileURLToPath(import.meta.url)));
  const candidates = [hookInput.cwd, process.env.CLAUDE_PROJECT_DIR, scriptRoot];
  return candidates.find((dir) => dir && existsSync(join(dir, ".agents", "skills"))) ?? scriptRoot;
}

// 外部境界: フック標準入力のJSONパース失敗は空入力として扱う（手動実行時は標準入力が無い）
// External boundary: treat hook stdin JSON parse failures as empty input (manual runs have no stdin).
function readHookInput() {
  if (process.stdin.isTTY) return {};
  try {
    return JSON.parse(readFileSync(0, "utf8"));
  } catch (error) {
    console.error(`[skill-name-collision-check] フック入力を読めず空入力として続行: ${error.message}`);
    return {};
  }
}
