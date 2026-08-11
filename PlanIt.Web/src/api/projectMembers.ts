import { seedProjectMembers, seedUsers } from "./seedData";
import { delay, mutate, MockApiError } from "./mockClient";
import type { Guid, ProjectMember, ProjectMemberRole, User } from "../types/domain";

export interface ProjectMemberWithUser extends ProjectMember {
  user: User;
}

export async function fetchProjectMembers(projectId: Guid): Promise<ProjectMemberWithUser[]> {
  const members = seedProjectMembers
    .filter((m) => m.projectId === projectId)
    .map((m) => {
      const user = seedUsers.find((u) => u.id === m.userId);
      if (!user) throw new MockApiError(`User ${m.userId} not found`, 404);
      return { ...m, user };
    });
  return delay(members);
}

export async function addProjectMember(
  projectId: Guid,
  userId: Guid,
  role: ProjectMemberRole = "Member",
): Promise<ProjectMemberWithUser> {
  return mutate(() => {
    const already = seedProjectMembers.find((m) => m.projectId === projectId && m.userId === userId);
    if (already) throw new MockApiError("User is already a collaborator on this project.", 409);
    const user = seedUsers.find((u) => u.id === userId);
    if (!user) throw new MockApiError(`User ${userId} not found`, 404);
    const member: ProjectMember = { projectId, userId, role, joinedAt: new Date().toISOString() };
    seedProjectMembers.push(member);
    return { ...member, user };
  });
}

export async function removeProjectMember(projectId: Guid, userId: Guid): Promise<void> {
  return mutate(() => {
    const idx = seedProjectMembers.findIndex((m) => m.projectId === projectId && m.userId === userId);
    if (idx === -1) throw new MockApiError("Membership not found.", 404);
    if (seedProjectMembers[idx].role === "Owner") {
      const otherOwners = seedProjectMembers.some(
        (m) => m.projectId === projectId && m.role === "Owner" && m.userId !== userId,
      );
      if (!otherOwners) {
        throw new MockApiError("A project must keep at least one Owner.", 400);
      }
    }
    seedProjectMembers.splice(idx, 1);
  });
}
