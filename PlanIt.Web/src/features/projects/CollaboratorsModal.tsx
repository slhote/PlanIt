import { useEffect, useState } from "react";
import { Modal } from "../../components/Modal";
import { initials } from "../../components/initials";
import { useAuth } from "../../auth/useAuth";
import { useProjectMembersQuery, useUserSearchQuery } from "../../hooks/queries";
import { useAddProjectMemberMutation, useRemoveProjectMemberMutation } from "../../hooks/mutations";
import type { Guid } from "../../types/domain";

const SEARCH_DEBOUNCE_MS = 300;

export function CollaboratorsModal({ projectId, onClose }: { projectId: Guid; onClose: () => void }) {
  const { user } = useAuth();
  const membersQuery = useProjectMembersQuery(projectId);
  const addMember = useAddProjectMemberMutation(projectId);
  const removeMember = useRemoveProjectMemberMutation(projectId);
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedQuery(query), SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [query]);

  const searchQuery = useUserSearchQuery(debouncedQuery);

  const members = membersQuery.data ?? [];
  const isOwner = members.some((m) => m.userId === user?.id && m.role === "Owner");
  const memberIds = new Set(members.map((m) => m.userId));

  const searchResults = (searchQuery.data ?? []).filter((u) => !memberIds.has(u.id));

  return (
    <Modal title="Collaborators" onClose={onClose}>
      {membersQuery.isLoading && (
        <div className="loading-state">
          <div className="spinner" />
        </div>
      )}

      {members.length > 0 && (
        <div className="stack" style={{ marginBottom: "var(--space-5)" }}>
          {members.map((m) => (
            <div key={m.userId} className="row-between">
              <span className="row">
                <span className="avatar avatar-sm">{initials(m.user.username)}</span>
                <span>
                  <div className="card-title">{m.user.username}</div>
                  <div className="card-meta">{m.role}</div>
                </span>
              </span>
              {isOwner && m.userId !== user?.id && (
                <button
                  type="button"
                  className="btn btn-ghost btn-sm"
                  disabled={removeMember.isPending}
                  onClick={() => removeMember.mutate(m.userId)}
                >
                  Remove
                </button>
              )}
            </div>
          ))}
        </div>
      )}

      {removeMember.isError && <p className="field-error">{(removeMember.error as Error).message}</p>}

      {isOwner ? (
        <div className="field">
          <label htmlFor="collaborator-search">Add a collaborator</label>
          <input
            id="collaborator-search"
            className="input"
            placeholder="Search by username or email…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          {query.trim() && !searchQuery.isFetching && searchResults.length === 0 && (
            <p className="field-hint">No matching users, or they're already a collaborator.</p>
          )}
          {searchResults.length > 0 && (
            <div className="stack" style={{ marginTop: "var(--space-3)" }}>
              {searchResults.map((u) => (
                <div key={u.id} className="row-between">
                  <span className="row">
                    <span className="avatar avatar-sm">{initials(u.username)}</span>
                    <span className="card-title">{u.username}</span>
                  </span>
                  <button
                    type="button"
                    className="btn btn-secondary btn-sm"
                    disabled={addMember.isPending}
                    onClick={() => {
                      addMember.mutate({ userId: u.id }, { onSuccess: () => setQuery("") });
                    }}
                  >
                    Add
                  </button>
                </div>
              ))}
            </div>
          )}
          {addMember.isError && <p className="field-error">{(addMember.error as Error).message}</p>}
        </div>
      ) : (
        <p className="field-hint">Only project owners can add or remove collaborators.</p>
      )}
    </Modal>
  );
}
