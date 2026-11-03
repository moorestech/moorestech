// 持ち物グリッドの列数。ブロック側上限も共有
// Inventory grid column count; block grids share the cap
// src/app/tokens.css の --inventory-columns と同値で、必ず同時に変える（CSS側はパネル幅の計算に使う）
// Must equal --inventory-columns in src/app/tokens.css and change together with it (the CSS side feeds the panel width)
export const INVENTORY_GRID_COLUMNS = 10;
