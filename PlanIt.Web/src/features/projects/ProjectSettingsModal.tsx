import { Modal } from "../../components/Modal";
import { useRecomputeSimilarTasksMutation } from "../../hooks/mutations";
import type { Guid } from "../../types/domain";

export function ProjectSettingsModal({ projectId, onClose }: { projectId: Guid; onClose: () => void }) {
  const recompute = useRecomputeSimilarTasksMutation(projectId);

  return (
    <Modal title="Project settings" onClose={onClose}>
      <div className="field">
          <label>Similar tasks</label>
          <p className="field-hint">
            Recomputes AI-based similarity for every task in this project. Runs in the background —
            results may take a few minutes to appear.
          </p>
          
          {/*Todo: Should add some sort of cool-down period so the user doesn't just spam this repeatedly */}
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
    </Modal>
  );
}
