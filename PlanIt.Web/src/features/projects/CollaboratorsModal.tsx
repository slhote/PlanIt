import { useMemo, useState } from "react";
import { Modal } from "../../components/Modal";
import { initials } from "../../components/initials";
import { useAuth } from "../../auth/useAuth";
import { useProjectMembersQuery, useUsersQuery } from "../../hooks/queries";
import { useAddProjectMemberMutation, useRemoveProjectMemberMutation } from "../../hooks/mutations";
import type { Guid } from "../../types/domain";

export function CollaboratorsModal({ projectId, onClose }: { projectId: Guid; onClose: () => void }) {
  const { user } = useAuth();
  const membersQuery = useProjectMembersQuery(projectId);
  const usersQuery = useUsersQuery();
  const addMember = useAddProjectMemberMutation(projectId);
  const removeMember = useRemoveProjectMemberMutation(projectId);
  const [query, setQuery] = useState("");

  const members = membersQuery.data ?? [];
  const isOwner = members.some((m) => m.userId === user?.id && m.role === "Owner");
  const memberIds = new Set(members.map((m) => m.userId));

  const searchResults = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q || !usersQuery.data) return [];
    return usersQuery.data.filter(
      (u) => !memberIds.has(u.id) && (u.username.toLowerCase().includes(q) || u.email.toLowerCase().includes(q)),
    );
  }, [query, usersQuery.data, members]);

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
          {query.trim() && searchResults.length === 0 && (
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
