import { Button, Modal, Text, Title } from "@mantine/core";
import { useTopic, dispatchAction, Topics } from "@/bridge";
import { respondPayload, buttonColor } from "./modalLogic";

// uGUI OneButtonModal の web 版。ui.modal トピックを購読し、要求があれば中央モーダルを描く。
// Web version of uGUI OneButtonModal; subscribes ui.modal and renders a centered modal on request.
export function ModalHost() {
  const data = useTopic(Topics.modal);

  // スナップショット未着、または表示対象が無ければ何も描かない。
  // Render nothing before the first snapshot or when there is no modal to show.
  if (!data || !data.modal) return null;
  const { id, title, message, buttonText, variant } = data.modal;

  // confirm/cancel を host へ送る。オーバーレイクリックは Modal.Root の onClose 経由で cancel。
  // Send confirm/cancel to the host; overlay clicks cancel via Modal.Root's onClose.
  const confirm = () => dispatchAction("ui.modal.respond", respondPayload(id, "confirm"));
  const cancel = () => dispatchAction("ui.modal.respond", respondPayload(id, "cancel"));

  // e2e が同期検証できるようトランジションは無効化する。
  // Disable transitions so e2e can assert synchronously.
  return (
    <Modal.Root opened onClose={() => void cancel()} centered transitionProps={{ duration: 0 }}>
      <Modal.Overlay data-testid="modal-backdrop" backgroundOpacity={0.6} />
      <Modal.Content data-testid="modal" w={320}>
        <Modal.Body p="lg">
          {/* タイトルと本文。uGUI の titleText / descriptionText に対応 */}
          {/* Title and body, mapping to uGUI titleText / descriptionText */}
          <Title order={2} size="h4" mb="xs">{title}</Title>
          <Text size="sm" c="dimmed" mb="lg">{message}</Text>
          <Button data-testid="modal-button" fullWidth color={buttonColor(variant)} onClick={() => void confirm()}>
            {buttonText}
          </Button>
        </Modal.Body>
      </Modal.Content>
    </Modal.Root>
  );
}
