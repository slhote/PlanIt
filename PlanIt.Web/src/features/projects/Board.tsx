import { useEffect, useState } from "react";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  TouchSensor,
  closestCenter,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import { SortableContext, rectSortingStrategy, useSortable, arrayMove } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { WorkItemCard } from "../../components/WorkItemCard";
import { statusLabel } from "../workitems/WorkItemForm";
import { WORK_ITEM_STATUSES } from "../../types/domain";
import type { Guid, User, WorkItem, WorkItemStatus } from "../../types/domain";

type ColumnMap = Record<WorkItemStatus, WorkItem[]>;

const ORDER_GAP = 1024;

/**
 * Computes the moved item's new fractional `order` from its neighbors in the destination
 * column's final arrangement — the midpoint between the items on either side, or ±ORDER_GAP at a
 * boundary. Matches the server's own assignment scheme (planit-api-contracts-backend.md §6) so a
 * freshly-created item and a freshly-moved item interleave correctly without a refetch.
 */
export function computeNewOrder(orderedItems: WorkItem[], movedItemId: Guid): number {
  const index = orderedItems.findIndex((item) => item.id === movedItemId);
  if (index === -1) return ORDER_GAP;

  const prev = orderedItems[index - 1];
  const next = orderedItems[index + 1];
  if (prev && next) return (prev.order + next.order) / 2;
  if (prev) return prev.order + ORDER_GAP;
  if (next) return next.order - ORDER_GAP;
  return ORDER_GAP;
}

function SortableCard({
  item,
  assignee,
  subtaskProgress,
  onOpen,
}: {
  item: WorkItem;
  assignee?: User;
  subtaskProgress?: { done: number; total: number };
  onOpen: () => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: item.id,
    data: { item },
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
    touchAction: "none" as const,
  };

  return (
    <div ref={setNodeRef} style={style} onClick={onOpen} {...listeners} {...attributes}>
      <WorkItemCard item={item} assignee={assignee} subtaskProgress={subtaskProgress} />
    </div>
  );
}

function Column({
  status,
  items,
  assigneeOf,
  subtaskProgressOf,
  onOpenItem,
}: {
  status: WorkItemStatus;
  items: WorkItem[];
  assigneeOf: (item: WorkItem) => User | undefined;
  subtaskProgressOf: (item: WorkItem) => { done: number; total: number } | undefined;
  onOpenItem: (item: WorkItem) => void;
}) {
  // Lets an empty column (or the gap after the last card) still accept a drop.
  const { setNodeRef, isOver } = useDroppable({ id: status });

  return (
    <div className="board-column">
      <div className="board-column-header">
        <span>{statusLabel(status)}</span>
        <span className="board-column-count">{items.length}</span>
      </div>
      <div
        ref={setNodeRef}
        className="board-column-body"
        style={{ background: isOver ? "var(--color-accent-tint)" : undefined, borderRadius: "var(--radius-md)" }}
      >
        <SortableContext items={items.map((i) => i.id)} strategy={rectSortingStrategy}>
          {items.length === 0 && (
            <div className="muted" style={{ padding: "var(--space-3) var(--space-2)", fontSize: "var(--font-size-sm)" }}>
              Drop cards here
            </div>
          )}
          {items.map((item) => (
            <SortableCard
              key={item.id}
              item={item}
              assignee={assigneeOf(item)}
              subtaskProgress={subtaskProgressOf(item)}
              onOpen={() => onOpenItem(item)}
            />
          ))}
        </SortableContext>
      </div>
    </div>
  );
}

export function Board({
  itemsByStatus,
  assigneeOf,
  subtaskProgressOf,
  onOpenItem,
  onStatusChange,
  onReorder,
}: {
  itemsByStatus: ColumnMap;
  assigneeOf: (item: WorkItem) => User | undefined;
  subtaskProgressOf: (item: WorkItem) => { done: number; total: number } | undefined;
  onOpenItem: (item: WorkItem) => void;
  onStatusChange: (item: WorkItem, newStatus: WorkItemStatus) => void;
  /** orderedItems is the destination column's final order after the move; movedItemId identifies
   * which one to persist a new `order` for — its neighbors in orderedItems are used to compute a
   * fractional midpoint (planit-api-contracts-backend.md §6), no other item's order changes. */
  onReorder: (orderedItems: WorkItem[], movedItemId: Guid) => void;
}) {
  // Board owns the live drag arrangement locally (standard dnd-kit multi-container
  // pattern) so cards can visually move between columns mid-drag; it resyncs from
  // the query-driven props whenever the server truth changes (including reverts).
  const [columns, setColumns] = useState<ColumnMap>(itemsByStatus);
  useEffect(() => {
    setColumns(itemsByStatus);
  }, [itemsByStatus]);

  const [activeItem, setActiveItem] = useState<WorkItem | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
  );

  function findContainer(id: string | number): WorkItemStatus | undefined {
    if (WORK_ITEM_STATUSES.includes(id as WorkItemStatus)) return id as WorkItemStatus;
    return WORK_ITEM_STATUSES.find((status) => columns[status].some((item) => item.id === id));
  }

  function handleDragStart(event: DragStartEvent) {
    setActiveItem((event.active.data.current?.item as WorkItem | undefined) ?? null);
  }

  function handleDragOver(event: DragOverEvent) {
    const { active, over } = event;
    if (!over) return;
    const activeContainer = findContainer(active.id);
    const overContainer = findContainer(over.id);
    if (!activeContainer || !overContainer || activeContainer === overContainer) return;

    setColumns((prev) => {
      const activeItems = prev[activeContainer];
      const overItems = prev[overContainer];
      const activeIndex = activeItems.findIndex((i) => i.id === active.id);
      if (activeIndex === -1) return prev;
      const movedItem = activeItems[activeIndex];
      const overIndex = overItems.findIndex((i) => i.id === over.id);
      const insertIndex = overIndex >= 0 ? overIndex : overItems.length;
      return {
        ...prev,
        [activeContainer]: activeItems.filter((i) => i.id !== active.id),
        [overContainer]: [...overItems.slice(0, insertIndex), movedItem, ...overItems.slice(insertIndex)],
      };
    });
  }

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event;
    setActiveItem(null);
    if (!over) return;

    const activeContainer = findContainer(active.id);
    const overContainer = findContainer(over.id);
    if (!activeContainer || !overContainer) return;

    let finalColumns = columns;
    let positionChanged = activeContainer !== overContainer;
    if (activeContainer === overContainer) {
      const items = columns[activeContainer];
      const activeIndex = items.findIndex((i) => i.id === active.id);
      const overIndex = items.findIndex((i) => i.id === over.id);
      if (activeIndex !== -1 && overIndex !== -1 && activeIndex !== overIndex) {
        finalColumns = { ...columns, [activeContainer]: arrayMove(items, activeIndex, overIndex) };
        setColumns(finalColumns);
        positionChanged = true;
      }
    }

    const movedItem = finalColumns[overContainer].find((i) => i.id === active.id);
    if (movedItem && movedItem.status !== overContainer) {
      onStatusChange(movedItem, overContainer);
    }

    // Only the destination column's final order matters — the source column's remaining items
    // keep their existing `order` values unchanged (that's the point of fractional indexing, no
    // sibling renumbering needed when an item leaves).
    if (positionChanged) {
      onReorder(finalColumns[overContainer], active.id as Guid);
    }
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragStart={handleDragStart}
      onDragOver={handleDragOver}
      onDragEnd={handleDragEnd}
    >
      <div className="board">
        {WORK_ITEM_STATUSES.map((status) => (
          <Column
            key={status}
            status={status}
            items={columns[status]}
            assigneeOf={assigneeOf}
            subtaskProgressOf={subtaskProgressOf}
            onOpenItem={onOpenItem}
          />
        ))}
      </div>
      <DragOverlay>
        {activeItem && (
          <WorkItemCard item={activeItem} assignee={assigneeOf(activeItem)} subtaskProgress={subtaskProgressOf(activeItem)} />
        )}
      </DragOverlay>
    </DndContext>
  );
}
