import { apiFetch } from "./httpClient";
import type { Guid, WorkItem, WorkItemStatus, WorkItemType } from "../types/domain";

export async function fetchWorkItem(projectId: Guid, id: Guid): Promise<WorkItem> {
  return apiFetch<WorkItem>(`/projects/${projectId}/workitems/${id}`);
}

export interface FeatureDetail {
  feature: WorkItem;
  childTasks: WorkItem[];
}

export async function fetchFeature(projectId: Guid, featureId: Guid): Promise<FeatureDetail> {
  return apiFetch<FeatureDetail>(`/projects/${projectId}/workitems/${featureId}/children`);
}

export interface CreateWorkItemInput {
  workItemType: WorkItemType;
  projectId: Guid;
  parentId: Guid | null;
  title: string;
  description: string | null;
  // Accepted for UI-shape convenience (WorkItemForm always submits it), but not sent to the
  // server — a new work item always starts ToDo, matching CreateWorkItemRequest's real shape,
  // which has no status field at all.
  status: WorkItemStatus;
  assigneeId: Guid | null;
  tags: string[];
}

export async function createWorkItem(input: CreateWorkItemInput): Promise<WorkItem> {
  return apiFetch<WorkItem>(`/projects/${input.projectId}/workitems`, {
    method: "POST",
    body: JSON.stringify({
      id: crypto.randomUUID(), // client-generated GUID, server upserts (idempotent create)
      workItemType: input.workItemType,
      parentId: input.parentId,
      title: input.title,
      description: input.description,
      assigneeId: input.assigneeId,
      tags: input.tags,
    }),
  });
}

export interface UpdateWorkItemInput {
  title?: string;
  description?: string | null;
  status?: WorkItemStatus;
  assigneeId?: Guid | null;
  tags?: string[];
  order?: number;
}

export async function updateWorkItem(projectId: Guid, id: Guid, patch: UpdateWorkItemInput): Promise<WorkItem> {
  return apiFetch<WorkItem>(`/projects/${projectId}/workitems/${id}`, {
    method: "PATCH",
    body: JSON.stringify(patch),
  });
}

export async function deleteWorkItem(projectId: Guid, id: Guid): Promise<{ deletedIds: Guid[] }> {
  return apiFetch<{ deletedIds: Guid[] }>(`/projects/${projectId}/workitems/${id}`, {
    method: "DELETE",
  });
}
