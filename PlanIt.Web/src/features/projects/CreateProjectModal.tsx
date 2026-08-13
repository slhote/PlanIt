import { useState, type FormEvent } from "react";
import { Modal } from "../../components/Modal";
import { useCreateProjectMutation } from "../../hooks/mutations";

export function CreateProjectModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (projectId: string) => void;
}) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const createProject = useCreateProjectMutation();

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;
    createProject.mutate(
      { name: name.trim(), description: description.trim() || null },
      { onSuccess: (project) => onCreated(project.id) },
    );
  }

  return (
    <Modal title="Create a project" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <div className="field">
          <label htmlFor="project-name">Name</label>
          <input
            id="project-name"
            className="input"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="e.g. Marketing site revamp"
            autoFocus
            required
          />
        </div>
        <div className="field">
          <label htmlFor="project-description">Description (optional)</label>
          <textarea
            id="project-description"
            className="textarea"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="What's this project about?"
          />
        </div>
        {createProject.isError && <p className="field-error">Couldn't create the project. Try again.</p>}
        <div className="thumb-bar" style={{ padding: 0, marginTop: "var(--space-2)" }}>
          <button type="button" className="btn btn-ghost" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary btn-block" disabled={createProject.isPending || !name.trim()}>
            {createProject.isPending ? "Creating…" : "Create project"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
