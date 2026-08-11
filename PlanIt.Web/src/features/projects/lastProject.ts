const KEY = "planit:lastProjectId";

export function getLastProjectId(): string | null {
  return localStorage.getItem(KEY);
}

export function setLastProjectId(projectId: string): void {
  localStorage.setItem(KEY, projectId);
}

export function clearLastProjectId(): void {
  localStorage.removeItem(KEY);
}
