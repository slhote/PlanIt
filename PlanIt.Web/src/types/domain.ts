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
  createdAt: string;
  updatedAt: string;
}

export const MAX_TAGS_PER_WORK_ITEM = 3;
