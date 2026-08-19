import { Modal } from "../../components/Modal";
import { useAuth } from "../../auth/useAuth";
import { useProjectMembersQuery } from "../../hooks/queries";
import { useRecomputeSimilarTasksMutation } from "../../hooks/mutations";
import type { Guid } from "../../types/domain";

// Owner-gated purely client-side, same convention CollaboratorsModal already uses -- there's no
// Owner-vs-Member authorization check on the backend for this endpoint either
// (planit-similar-tasks-semantic-embeddings.md).
export function ProjectSettingsModal({ projectId, onClose }: { projectId: Guid; onClose: () => void }) {
  const { user } = useAuth();
  const membersQuery = useProjectMembersQuery(projectId);
  const recompute = useRecomputeSimilarTasksMutation(projectId);

  const members = membersQuery.data ?? [];
  const isOwner = members.some((m) => m.userId === user?.id && m.role === "Owner");

  return (
    <Modal title="Project settings" onClose={onClose}>
      {isOwner ? (
        <div className="field">
          <label>Similar tasks</label>
          <p className="field-hint">
            Recomputes AI-based similarity for every task in this project. Runs in the background —
            results may take a few minutes to appear.
          </p>
          <button
            type="button"
            className="btn btn-secondary btn-sm"
            disabled={recompute.isPending}
            onClick={() => recompute.mutate()}
          >
            {recompute.isPending ? "Enqueuing…" : "Recompute similar tasks"}
          </button>
          {recompute.isSuccess && (
            <p className="field-hint">Enqueued {recompute.data.enqueuedCount} tasks.</p>
          )}
          {recompute.isError && <p className="field-error">{(recompute.error as Error).message}</p>}
        </div>
      ) : (
        <p className="field-hint">Only project owners can access project settings.</p>
      )}
    </Modal>
  );
}
