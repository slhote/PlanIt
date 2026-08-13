import { apiFetch } from "./httpClient";
import type { Guid, ProjectMember, ProjectMemberRole, User } from "../types/domain";

export interface ProjectMemberWithUser extends ProjectMember {
  user: User;
}

export async function fetchProjectMembers(projectId: Guid): Promise<ProjectMemberWithUser[]> {
  return apiFetch<ProjectMemberWithUser[]>(`/projects/${projectId}/members`);
}

export async function addProjectMember(
  projectId: Guid,
  userId: Guid,
  role: ProjectMemberRole = "Member",
): Promise<ProjectMemberWithUser> {
  return apiFetch<ProjectMemberWithUser>(`/projects/${projectId}/members`, {
    method: "POST",
    body: JSON.stringify({ userId, role }),
  });
}

export async function removeProjectMember(projectId: Guid, userId: Guid): Promise<void> {
  return apiFetch<void>(`/projects/${projectId}/members/${userId}`, {
    method: "DELETE",
  });
}
