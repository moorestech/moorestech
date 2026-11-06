// 突き合わせの入力を git と作業ツリーから読む（IO はこのファイルだけ）
// Reads the matcher's inputs from git and the working tree (the only file doing IO)
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { extractWebTestTitles, isWebTestFile } from "./webTests.mjs";
import { extractWebTestTags } from "./nunitWebTags.mjs";

export const WEB_UI_ROOT = "moorestech_web/webui/";
const CS_ROOTS = ["moorestech_client/Assets/Scripts/", "moorestech_server/Assets/Scripts/"];

function git(repositoryRoot, args) {
  return execFileSync("git", ["-C", repositoryRoot, ...args], { encoding: "utf8", maxBuffer: 256 * 1024 * 1024 });
}

function listWorkingTree(repositoryRoot, roots) {
  // 未コミットの新規ファイルも含め、作業ツリーに実在するものだけを返す
  // Include uncommitted new files and return only files that exist in the working tree
  const listing = git(repositoryRoot, ["ls-files", "-z", "--cached", "--others", "--exclude-standard", "--", ...roots]);
  return listing.split("\0").filter((path) => path !== "" && existsSync(join(repositoryRoot, path)));
}

export function readWebTests(repositoryRoot, webRev) {
  // webRev が null なら作業ツリー、それ以外はそのコミットの Web を読む（撤去後も撤去前の名前で突き合わせるため）
  // A null webRev reads the working tree; otherwise read Web at that commit (to match against pre-removal names)
  const paths = webRev === null
    ? listWorkingTree(repositoryRoot, [WEB_UI_ROOT])
    : git(repositoryRoot, ["ls-tree", "-r", "-z", "--name-only", webRev, "--", WEB_UI_ROOT]).split("\0").filter((path) => path !== "");
  const tests = [];
  for (const path of paths) {
    const webPath = path.slice(WEB_UI_ROOT.length);
    if (!isWebTestFile(webPath)) continue;
    const text = webRev === null ? readFileSync(join(repositoryRoot, path), "utf8") : git(repositoryRoot, ["show", `${webRev}:${path}`]);
    for (const found of extractWebTestTitles(text)) tests.push({ file: webPath, title: found.title, line: found.line });
  }
  return tests;
}

export function readWebTestTags(repositoryRoot) {
  // C# の全テストから <web-test> を集め、書式の誤りはパスと行番号付きで返す
  // Collect <web-test> from all C# sources; malformed tags come back with path and line
  const tags = [];
  const errors = [];
  for (const path of listWorkingTree(repositoryRoot, CS_ROOTS).filter((candidate) => candidate.endsWith(".cs"))) {
    const text = readFileSync(join(repositoryRoot, path), "utf8");
    if (!text.includes("<web-test")) continue;
    const found = extractWebTestTags(text);
    for (const tag of found.tags) tags.push({ ...tag, csFile: path });
    for (const error of found.errors) errors.push(`${path}:${error.line}: <web-test> の書式が違う: ${error.text}`);
  }
  return { tags, errors };
}
