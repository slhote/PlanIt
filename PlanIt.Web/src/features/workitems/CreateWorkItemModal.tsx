import { Modal } from "../../components/Modal";
import { WorkItemForm } from "./WorkItemForm";
import { useProjectBoardQuery, useProjectMembersQuery } from "../../hooks/queries";
import { useCreateWorkItemMutation } from "../../hooks/mutations";
import type { Guid, WorkItemType } from "../../types/domain";

export function CreateWorkItemModal({
  projectId,
  lockedType,
  lockedParentId,
  onClose,
  onCreated,
}: {
  projectId: Guid;
  lockedType?: WorkItemType;
  lockedParentId?: Guid | null;
  onClose: () => void;
  onCreated?: (workItemId: Guid) => void;
}) {
  const boardQuery = useProjectBoardQuery(projectId);
  const membersQuery = useProjectMembersQuery(projectId);
  const createWorkItem = useCreateWorkItemMutation(projectId);

  const featureOptions = (boardQuery.data?.workItems ?? []).filter((w) => w.workItemType === "Feature");

  return (
    <Modal title={lockedType === "Task" && lockedParentId ? "Add task" : "New work item"} onClose={onClose}>
      <WorkItemForm
        mode="create"
        lockedType={lockedType}
        lockedParentId={lockedParentId}
        featureOptions={featureOptions}
        members={membersQuery.data ?? []}
        submitting={createWorkItem.isPending}
        submitError={createWorkItem.isError ? (createWorkItem.error as Error).message : undefined}
        onCancel={onClose}
        onSubmit={(values) =>
          createWorkItem.mutate(
            { ...values, projectId },
            {
              onSuccess: (created) => {
                onCreated?.(created.id);
                onClose();
              },
            },
          )
        }
      />
    </Modal>
  );
}
