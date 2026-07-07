// asmdef ごとの色と「層」順。既知の asmdef は固定値、未知のものはパレット/既定層にフォールバックする。
// Per-asmdef color and layer order. Known asmdefs use fixed values; unknown ones fall back to a palette / default layer.

// group(列キー)は C# の asmdef 名(ドット区切り)、または TS スライス `pkg/slice`("/"を含む)。
// 既知 asmdef は固定色/層、TS スライスは "/" 判定で専用色/層、その他はパレット/既定層へフォールバック。
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
// TS スライス("/"を含む group)用パレット。cyan/teal/blue 系の family で「TS 側」を視覚的に束ねる。
const TS_PALETTE = ['#22D3EE', '#2DD4BF', '#38BDF8', '#34D399', '#60A5FA', '#5EEAD4', '#7DD3FC', '#A5B4FC'];
function hash(s: string): number {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return h;
}
export const asmColor = (a: string): string => {
  if (a in ASM_COLOR) return ASM_COLOR[a];
  if (a.includes('/')) return TS_PALETTE[hash(a) % TS_PALETTE.length];
  return PALETTE[hash(a) % PALETTE.length];
};

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

// TS スライス(`pkg/slice`)は C# 群(既知 0-7 / 未知 50)の右(60+)へ。基盤(shared/bridge)→features→app の順。
function tsLayer(a: string): number {
  const base = 60;
  const s = a.toLowerCase();
  if (/(^|\/)(shared|lib|common|utils?)(\/|$)/.test(s)) return base + 0;
  if (/(^|\/)(bridge|api|store|stores|hooks?|services?)(\/|$)/.test(s)) return base + 1;
  if (/(^|\/)(components?|ui|widgets?)(\/|$)/.test(s)) return base + 2;
  if (/(^|\/)features?(\/|$)/.test(s)) return base + 5;
  if (/(^|\/)(pages?|routes?|views?)(\/|$)/.test(s)) return base + 6;
  if (/(^|\/)(app|main|root)(\/|$)/.test(s)) return base + 8;
  return base + 4;
}
// 未知 asmdef は既知の後ろ(50)へ。TS スライス("/"含む)は tsLayer で 60+。
export const asmLayer = (a: string): number => (a in ASM_LAYER ? ASM_LAYER[a] : a.includes('/') ? tsLayer(a) : 50);

// Dep-Map 列の大分類。Server(C#非Client) → Client(`Client.*`) → TS スライス("/"含む) の3群順。
export const superGroup = (a: string): 0 | 1 | 2 => (a.includes('/') ? 2 : a.startsWith('Client.') ? 1 : 0);
