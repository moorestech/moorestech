// 通知の重要度。error=失敗/契約違反, info=成功/軽微。ToastHost が色分けに使う
// Notification severity; error=failure/contract violation, info=success/minor. ToastHost colors by this
export type NotifyVariant = "error" | "info";

// bridge から UI への一方向通知 sink。bridge は features を import しないため、
// 実体（toast store）は起動時に features 側から注入する。
// One-way notify sink from bridge to UI. bridge never imports features,
// so the concrete sink (toast store) is injected from the feature side at startup.
let sink: (message: string, variant: NotifyVariant) => void = () => {};

export function setToastSink(fn: (message: string, variant: NotifyVariant) => void) {
  sink = fn;
}

export function notify(message: string, variant: NotifyVariant) {
  sink(message, variant);
}
