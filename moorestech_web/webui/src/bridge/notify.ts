// bridge から UI への一方向通知 sink。bridge は features を import しないため、
// 実体（toast store）は起動時に features 側から注入する。
// One-way notify sink from bridge to UI. bridge never imports features,
// so the concrete sink (toast store) is injected from the feature side at startup.
let sink: (message: string) => void = () => {};

export function setToastSink(fn: (message: string) => void) {
  sink = fn;
}

export function notify(message: string) {
  sink(message);
}
