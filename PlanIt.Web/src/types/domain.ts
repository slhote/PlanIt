export type Guid = string;

export interface User {
  id: Guid;
  username: string;
  email: string;
  createdAt: string;
}

export interface Project {
  id: Guid;
  name: string;
  description: string | null;
  createdByUserId: Guid;
  createdAt: string;
}

export type ProjectMemberRole = "Owner" | "Member";

export interface ProjectMember {
  projectId: Guid;
  userId: Guid;
  role: ProjectMemberRole;
  joinedAt: string;
}

export type WorkItemType = "Feature" | "Task";

export type WorkItemStatus = "ToDo" | "InProgress" | "Completed";

export const WORK_ITEM_STATUSES: WorkItemStatus[] = ["ToDo", "InProgress", "Completed"];

export interface WorkItem {
  id: Guid;
  workItemType: WorkItemType;
  projectId: Guid;
  parentId: Guid | null;
  title: string;
  description: string | null;
  status: WorkItemStatus;
  assigneeId: Guid | null;
  tags: string[];
  // Fractional-index sort key, meaningful only within a (projectId, parentId, status) group —
  // one board column. The server assigns it on create and a drag-and-drop move updates it via a
  // single-item PATCH (planit-api-contracts-backend.md §6) — there's no bulk reorder endpoint.
  order: number;
  createdAt: string;
  updatedAt: string;
}

export const MAX_TAGS_PER_WORK_ITEM = 3;

// Fixed tag vocabulary for the tag picker's suggestions — a frontend-only concept (not server
// data), moved here from the now-retired api/seedData.ts.
export const ALL_TAGS = ["frontend", "backend", "design", "bug", "urgent"] as const;
