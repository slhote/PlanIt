import { useState, type FormEvent } from "react";
import { TagInput } from "../../components/TagInput";
import { ALL_TAGS } from "../../api/seedData";
import { WORK_ITEM_STATUSES } from "../../types/domain";
import type { Guid, WorkItem, WorkItemStatus, WorkItemType } from "../../types/domain";
import type { ProjectMemberWithUser } from "../../api/projectMembers";

export interface WorkItemFormValues {
  workItemType: WorkItemType;
  parentId: Guid | null;
  title: string;
  description: string | null;
  status: WorkItemStatus;
  assigneeId: Guid | null;
  tags: string[];
}

export function WorkItemForm({
  mode,
  initial,
  lockedType,
  lockedParentId,
  featureOptions,
  members,
  onSubmit,
  onCancel,
  submitting,
  submitError,
}: {
  mode: "create" | "edit";
  initial?: Partial<WorkItemFormValues>;
  /** When set, the type selector is hidden (e.g. "add task to this feature" always creates a Task). */
  lockedType?: WorkItemType;
  /** When set (including null), the parent selector is hidden and this value is always used. */
  lockedParentId?: Guid | null;
  featureOptions: WorkItem[];
  members: ProjectMemberWithUser[];
  onSubmit: (values: WorkItemFormValues) => void;
  onCancel: () => void;
  submitting: boolean;
  submitError?: string;
}) {
  const [workItemType, setWorkItemType] = useState<WorkItemType>(lockedType ?? initial?.workItemType ?? "Task");
  const [parentId, setParentId] = useState<Guid | null>(
    lockedParentId !== undefined ? lockedParentId : initial?.parentId ?? null,
  );
  const [title, setTitle] = useState(initial?.title ?? "");
  const [description, setDescription] = useState(initial?.description ?? "");
  const [status, setStatus] = useState<WorkItemStatus>(initial?.status ?? "ToDo");
  const [assigneeId, setAssigneeId] = useState<Guid | null>(initial?.assigneeId ?? null);
  const [tags, setTags] = useState<string[]>(initial?.tags ?? []);

  const showTypeSelector = lockedType === undefined;
  const showParentSelector = lockedParentId === undefined && workItemType === "Task";

  function handleTypeChange(next: WorkItemType) {
    setWorkItemType(next);
    if (next === "Feature") setParentId(null);
  }

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;
    onSubmit({
      workItemType,
      parentId: workItemType === "Feature" ? null : parentId,
      title: title.trim(),
      description: description.trim() || null,
      status,
      assigneeId,
      tags,
    });
  }

  return (
    <form onSubmit={handleSubmit}>
      {showTypeSelector && (
        <div className="field">
          <label htmlFor="wi-type">Type</label>
          <select
            id="wi-type"
            className="select"
            value={workItemType}
            onChange={(e) => handleTypeChange(e.target.value as WorkItemType)}
          >
            <option value="Feature">Feature</option>
            <option value="Task">Task</option>
          </select>
        </div>
      )}

      {showParentSelector && (
        <div className="field">
          <label htmlFor="wi-parent">Parent feature</label>
          <select
            id="wi-parent"
            className="select"
            value={parentId ?? ""}
            onChange={(e) => setParentId(e.target.value || null)}
          >
            <option value="">No feature (top-level task)</option>
            {featureOptions.map((f) => (
              <option key={f.id} value={f.id}>
                {f.title}
              </option>
            ))}
          </select>
        </div>
      )}

      <div className="field">
        <label htmlFor="wi-title">Title</label>
        <input
          id="wi-title"
          className="input"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          maxLength={200}
          autoFocus
          required
        />
      </div>

      <div className="field">
        <label htmlFor="wi-description">Description</label>
        <textarea
          id="wi-description"
          className="textarea"
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          maxLength={4000}
        />
      </div>

      <div className="field">
        <label htmlFor="wi-status">Status</label>
        <select id="wi-status" className="select" value={status} onChange={(e) => setStatus(e.target.value as WorkItemStatus)}>
          {WORK_ITEM_STATUSES.map((s) => (
            <option key={s} value={s}>
              {statusLabel(s)}
            </option>
          ))}
        </select>
      </div>

      <div className="field">
        <label htmlFor="wi-assignee">Assignee</label>
        <select
          id="wi-assignee"
          className="select"
          value={assigneeId ?? ""}
          onChange={(e) => setAssigneeId(e.target.value || null)}
        >
          <option value="">Unassigned</option>
          {members.map((m) => (
            <option key={m.userId} value={m.userId}>
              {m.user.username}
            </option>
          ))}
        </select>
      </div>

      <div className="field">
        <label>Tags</label>
        <TagInput tags={tags} onChange={setTags} suggestions={ALL_TAGS} />
        <span className="field-hint">Up to 3 tags.</span>
      </div>

      {submitError && <p className="field-error">{submitError}</p>}

      <div className="thumb-bar" style={{ padding: 0, marginTop: "var(--space-2)" }}>
        <button type="button" className="btn btn-ghost" onClick={onCancel}>
          Cancel
        </button>
        <button type="submit" className="btn btn-primary btn-block" disabled={submitting || !title.trim()}>
          {submitting ? "Saving…" : mode === "create" ? "Create" : "Save changes"}
        </button>
      </div>
    </form>
  );
}

export function statusLabel(status: WorkItemStatus): string {
  if (status === "ToDo") return "To do";
  if (status === "InProgress") return "In progress";
  return "Completed";
}
