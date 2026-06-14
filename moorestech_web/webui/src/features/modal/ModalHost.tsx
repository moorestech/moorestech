import { useTopic, dispatchAction, Topics } from "@/bridge";
import { respondPayload, buttonClass } from "./modalLogic";

// uGUI OneButtonModal の web 版。ui.modal トピックを購読し、要求があれば中央モーダルを描く。
// Web version of uGUI OneButtonModal; subscribes ui.modal and renders a centered modal on request.
export function ModalHost() {
  const data = useTopic(Topics.modal);

  // スナップショット未着、または表示対象が無ければ何も描かない。
  // Render nothing before the first snapshot or when there is no modal to show.
  if (!data || !data.modal) return null;
  const { id, title, message, buttonText, variant } = data.modal;

  // confirm/cancel を host へ送る。背景クリックは cancel、パネル内クリックは伝播停止。
  // Send confirm/cancel to the host; backdrop click cancels, panel click stops propagation.
  const confirm = () => dispatchAction("ui.modal.respond", respondPayload(id, "confirm"));
  const cancel = () => dispatchAction("ui.modal.respond", respondPayload(id, "cancel"));

  return (
    <div
      data-testid="modal-backdrop"
      onClick={cancel}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
    >
      <div
        data-testid="modal"
        role="dialog"
        onClick={(e) => e.stopPropagation()}
        className="w-80 rounded border border-gray-700 bg-gray-800 p-5 shadow-xl"
      >
        {/* タイトルと本文。uGUI の titleText / descriptionText に対応 */}
        {/* Title and body, mapping to uGUI titleText / descriptionText */}
        <h2 className="mb-2 text-lg font-bold text-gray-100">{title}</h2>
        <p className="mb-5 text-sm text-gray-300">{message}</p>
        <button data-testid="modal-button" onClick={confirm} className={buttonClass(variant)}>
          {buttonText}
        </button>
      </div>
    </div>
  );
}
