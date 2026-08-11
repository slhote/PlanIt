import { seedWorkItems } from "./seedData";
import { delay, mutate, nextId, MockApiError } from "./mockClient";
import type { Guid, WorkItem, WorkItemStatus, WorkItemType } from "../types/domain";
import { MAX_TAGS_PER_WORK_ITEM } from "../types/domain";

export async function fetchWorkItem(id: Guid): Promise<WorkItem> {
  const item = seedWorkItems.find((w) => w.id === id);
  if (!item) throw new MockApiError(`Work item ${id} not found`, 404);
  return delay({ ...item });
}

export interface FeatureDetail {
  feature: WorkItem;
  childTasks: WorkItem[];
}

export async function fetchFeature(featureId: Guid): Promise<FeatureDetail> {
  const feature = seedWorkItems.find((w) => w.id === featureId && w.workItemType === "Feature");
  if (!feature) throw new MockApiError(`Feature ${featureId} not found`, 404);
  return delay({
    feature: { ...feature },
    childTasks: seedWorkItems.filter((w) => w.parentId === featureId).map((w) => ({ ...w })),
  });
}

export interface CreateWorkItemInput {
  workItemType: WorkItemType;
  projectId: Guid;
  parentId: Guid | null;
  title: string;
  description: string | null;
  status: WorkItemStatus;
  assigneeId: Guid | null;
  tags: string[];
}

function normalizeTags(tags: string[]): string[] {
  const cleaned = Array.from(new Set(tags.map((t) => t.trim().toLowerCase()).filter(Boolean)));
  if (cleaned.length > MAX_TAGS_PER_WORK_ITEM) {
    throw new MockApiError(`A work item can have at most ${MAX_TAGS_PER_WORK_ITEM} tags`, 400);
  }
  return cleaned;
}

function assertHierarchyInvariant(workItemType: WorkItemType, parentId: Guid | null) {
  if (workItemType === "Feature" && parentId !== null) {
    throw new MockApiError("A Feature cannot have a parent — only Tasks can be nested under a Feature.", 400);
  }
  if (parentId !== null) {
    const parent = seedWorkItems.find((w) => w.id === parentId);
    if (!parent) throw new MockApiError(`Parent work item ${parentId} not found`, 404);
    if (parent.workItemType !== "Feature") {
      throw new MockApiError("A Task's parent must be a Feature.", 400);
    }
  }
}

export async function createWorkItem(input: CreateWorkItemInput): Promise<WorkItem> {
  return mutate(() => {
    assertHierarchyInvariant(input.workItemType, input.parentId);
    const now = new Date().toISOString();
    const item: WorkItem = {
      id: nextId("w"),
      workItemType: input.workItemType,
      projectId: input.projectId,
      parentId: input.parentId,
      title: input.title,
      description: input.description,
      status: input.status,
      assigneeId: input.assigneeId,
      tags: normalizeTags(input.tags),
      createdAt: now,
      updatedAt: now,
    };
    seedWorkItems.push(item);
    return { ...item };
  });
}

export interface UpdateWorkItemInput {
  title?: string;
  description?: string | null;
  status?: WorkItemStatus;
  assigneeId?: Guid | null;
  tags?: string[];
}

export async function updateWorkItem(id: Guid, patch: UpdateWorkItemInput): Promise<WorkItem> {
  return mutate(() => {
    const item = seedWorkItems.find((w) => w.id === id);
    if (!item) throw new MockApiError(`Work item ${id} not found`, 404);
    if (patch.title !== undefined) item.title = patch.title;
    if (patch.description !== undefined) item.description = patch.description;
    if (patch.status !== undefined) item.status = patch.status;
    if (patch.assigneeId !== undefined) item.assigneeId = patch.assigneeId;
    if (patch.tags !== undefined) item.tags = normalizeTags(patch.tags);
    item.updatedAt = new Date().toISOString();
    return { ...item };
  });
}

/** How many descendants deleting this item would take with it — used to drive the cascade-delete confirmation copy before the mutation is ever fired. */
export function countCascadeDeletions(id: Guid): number {
  const item = seedWorkItems.find((w) => w.id === id);
  if (!item) return 0;
  if (item.workItemType !== "Feature") return 0;
  return seedWorkItems.filter((w) => w.parentId === id).length;
}

/**
 * Persists a new within-column order for a status group. There's no `order`/
 * `position` field in the persistence schema yet — this is a gap the mock
 * surfaces rather than papers over. For the mock, order is just array
 * position: pull the reordered items out and reinsert them as a block at the
 * earliest index any of them occupied, so their relative position among
 * items from other statuses/projects stays stable.
 */
export async function reorderWorkItems(orderedIds: Guid[]): Promise<void> {
  return mutate(() => {
    const idSet = new Set(orderedIds);
    const itemsById = new Map<Guid, WorkItem>();
    let insertAt = -1;
    for (let i = seedWorkItems.length - 1; i >= 0; i--) {
      const item = seedWorkItems[i];
      if (idSet.has(item.id)) {
        itemsById.set(item.id, item);
        insertAt = i;
        seedWorkItems.splice(i, 1);
      }
    }
    if (insertAt === -1) return;
    const ordered = orderedIds.map((id) => itemsById.get(id)).filter((item): item is WorkItem => !!item);
    seedWorkItems.splice(insertAt, 0, ...ordered);
  }, 150);
}

export async function deleteWorkItem(id: Guid): Promise<{ deletedIds: Guid[] }> {
  return mutate(() => {
    const item = seedWorkItems.find((w) => w.id === id);
    if (!item) throw new MockApiError(`Work item ${id} not found`, 404);
    const cascadeIds =
      item.workItemType === "Feature" ? seedWorkItems.filter((w) => w.parentId === id).map((w) => w.id) : [];
    const deletedIds = [id, ...cascadeIds];
    for (const deletedId of deletedIds) {
      const idx = seedWorkItems.findIndex((w) => w.id === deletedId);
      if (idx !== -1) seedWorkItems.splice(idx, 1);
    }
    return { deletedIds };
  });
}
