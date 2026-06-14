// asmdef ごとの色と「層」順。既知の asmdef は固定値、未知のものはパレット/既定層にフォールバックする。
// Per-asmdef color and layer order. Known asmdefs use fixed values; unknown ones fall back to a palette / default layer.

// 既知の asmdef(対象プロジェクトに合わせて追記してよい)。色は依存境界を一目で見せるための割り当て。
export const ASM_COLOR: Record<string, string> = {
  'Game.CleanRoom': '#2563EB',
  'Game.Block': '#D97706',
  'Game.Block.Interface': '#7C3AED',
  'Core.Master': '#0891B2',
  'Game.SaveLoad': '#059669',
  'Server.Boot': '#E11D48',
  'Server.Tests': '#64748B',
  other: '#94A3B8',
};

// 未知 asmdef 用パレット(名前のハッシュで安定割り当て)。
const PALETTE = ['#2563EB', '#D97706', '#7C3AED', '#0891B2', '#059669', '#E11D48', '#DB2777', '#0D9488', '#CA8A04', '#475569'];
function hash(s: string): number {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return h;
}
export const asmColor = (a: string) => ASM_COLOR[a] ?? PALETTE[hash(a) % PALETTE.length];

// 依存の「層」順(左=被参照される基盤、右=利用側)。Dep-Map の列順に使う。
export const ASM_LAYER: Record<string, number> = {
  'Game.Block.Interface': 0,
  'Core.Master': 1,
  'Game.CleanRoom': 2,
  'Game.Block': 3,
  'Game.SaveLoad': 4,
  'Server.Boot': 5,
  'Server.Tests': 6,
  other: 7,
};
// 未知 asmdef は既知の後ろ(50)へ。列順を厳密に制御したい場合は上のマップに追記する。
export const asmLayer = (a: string) => ASM_LAYER[a] ?? 50;
