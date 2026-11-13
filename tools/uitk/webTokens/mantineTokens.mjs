// Mantine の styles.css から基底（:root,:host）・暗色の上書き・部品のクラスのブロックを読み、Web が使う --mantine-* と部品の寸法の名前と生の値を返す
// Reads the base (:root,:host), dark-override and component-class blocks from Mantine's styles.css and returns the --mantine-* and component size names the Web uses with their raw values
import { stripComments } from "./parseCssTokens.mjs";

const scales = ["xs", "sm", "md", "lg", "xl"];
const paletteNames = ["dark", "gray", "blue", "red", "green"];
const accentNames = ["blue", "red", "green"];
const semanticNames = ["text", "body", "dimmed", "placeholder", "error", "default", "default-hover", "default-color", "default-border", "disabled", "disabled-color"];
const baseSelector = /^:root\s*,\s*:host$/;
const darkSelector = /^:root\[data-mantine-color-scheme='dark'\]\s*,\s*:host\(\[data-mantine-color-scheme='dark'\]\)$/;

// Mantine の部品のクラスが自分に宣言する寸法の変数（Button・Input）。UITK の FormButton・GameTextField が Web と同じ名前で読む
// Size variables Mantine components declare on their own class (Button, Input); UITK FormButton and GameTextField read them under the Web names
const componentTokenBlocks = [
  { selector: ".m_77c9d27d", names: ["--button-height-sm", "--button-height-xl", "--button-padding-x-sm", "--button-padding-x-xl"] },
  { selector: ".m_6c018570", names: ["--input-height-sm"] },
];

// 出力する名前の一覧（出力順）。Web の CSS・features と UITK の部品が参照するものに限る
// Names to emit, in output order; limited to what the Web CSS/features and the UITK components reference
export function mantineTokenNames() {
  const names = ["--mantine-color-white", "--mantine-color-black"];
  for (const palette of paletteNames) for (let shade = 0; shade <= 9; shade++) names.push(`--mantine-color-${palette}-${shade}`);
  for (const semantic of semanticNames) names.push(`--mantine-color-${semantic}`);
  names.push("--mantine-primary-color-filled", "--mantine-primary-color-filled-hover");
  for (const accent of accentNames) names.push(`--mantine-color-${accent}-filled`, `--mantine-color-${accent}-filled-hover`, `--mantine-color-${accent}-text`, `--mantine-color-${accent}-light-color`);
  for (const kind of ["spacing", "radius", "font-size", "line-height"]) for (const scale of scales) names.push(`--mantine-${kind}-${scale}`);
  names.push("--mantine-radius-default", "--mantine-line-height");
  for (const block of componentTokenBlocks) names.push(...block.names);
  return names;
}

// 基底の宣言を読み、暗色の上書きを後から当てる（Web は MantineProvider defaultColorScheme="dark"）
// Read base declarations, then apply the dark overrides on top (the Web uses MantineProvider defaultColorScheme="dark")
export function readMantineRawTokens(cssText) {
  const raw = new Map();
  const darkBlocks = [];
  for (const match of stripComments(cssText).matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selector = match[1].trim().replace(/\s+/g, " ");
    if (baseSelector.test(selector)) addDeclarations(raw, match[2]);
    else if (darkSelector.test(selector)) darkBlocks.push(match[2]);
    else addComponentDeclarations(raw, selector, match[2]);
  }
  if (raw.size === 0 || darkBlocks.length === 0) throw new Error("Mantine styles.css has no :root,:host block or no dark override block");
  for (const block of darkBlocks) addDeclarations(raw, block);
  return raw;
}

// var(--x) だけの値を参照先の生の値まで辿る。辿れなければ止める
// Follow a bare var(--x) value to the referenced raw value; stop when it cannot be followed
export function resolveVarChain(raw, name) {
  let value = raw.get(name);
  const seen = new Set([name]);
  for (let reference = /^var\((--[\w-]+)\)$/.exec(value ?? ""); reference !== null; reference = /^var\((--[\w-]+)\)$/.exec(value)) {
    if (seen.has(reference[1]) || !raw.has(reference[1])) throw new Error(`${name}: cannot follow ${reference[1]}`);
    seen.add(reference[1]);
    value = raw.get(reference[1]);
  }
  if (value === undefined) throw new Error(`${name} is not declared in Mantine styles.css`);
  return value;
}

// 部品のクラスの宣言からは一覧の名前だけを取る（--input-padding のような要素ごとの式は取らない）。同じ名前が別の値で 2 度出たら止める
// Take only the listed names from a component class's declarations (not per-element formulas such as --input-padding); stop if a name appears twice with different values
function addComponentDeclarations(raw, selector, body) {
  const block = componentTokenBlocks.find((candidate) => candidate.selector === selector);
  if (block === undefined) return;
  const declared = new Map();
  addDeclarations(declared, body);
  for (const name of block.names) {
    if (!declared.has(name)) continue;
    if (raw.has(name) && raw.get(name) !== declared.get(name)) throw new Error(`${name} is declared twice with different values in ${selector}`);
    raw.set(name, declared.get(name));
  }
}

function addDeclarations(raw, body) {
  for (const declaration of body.split(";")) {
    const match = declaration.match(/^\s*(--[\w-]+)\s*:\s*([\s\S]+?)\s*$/);
    if (match) raw.set(match[1], match[2].replace(/\s+/g, " "));
  }
}
