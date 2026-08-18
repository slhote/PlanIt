import { Modal } from "../../components/Modal";
import { WorkItemTypeIcon } from "../../components/icons";
import type { WorkItemType } from "../../types/domain";

export function WorkItemTypePicker({
  onClose,
  onSelect,
}: {
  onClose: () => void;
  onSelect: (type: WorkItemType) => void;
}) {
  return (
    <Modal title="What do you want to add?" onClose={onClose}>
      <div className="stack">
        <button
          type="button"
          className="card card-interactive"
          style={{ textAlign: "left", width: "100%", cursor: "pointer" }}
          onClick={() => onSelect("Feature")}
        >
          <div className="row" style={{ gap: "var(--space-2)" }}>
            <WorkItemTypeIcon type="Feature" size={20} />
            <span className="card-title">Feature</span>
          </div>
          <p className="card-meta">A larger piece of work, broken into tasks.</p>
        </button>
        <button
          type="button"
          className="card card-interactive"
          style={{ textAlign: "left", width: "100%", cursor: "pointer" }}
          onClick={() => onSelect("Task")}
        >
          <div className="row" style={{ gap: "var(--space-2)" }}>
            <WorkItemTypeIcon type="Task" size={20} />
            <span className="card-title">Task</span>
          </div>
          <p className="card-meta">A single step — on its own or under a feature.</p>
        </button>
      </div>
    </Modal>
  );
}
