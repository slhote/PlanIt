import type { ReactNode } from "react";
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  TouchSensor,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import { useState } from "react";
import { WorkItemCard } from "../../components/WorkItemCard";
import { statusLabel } from "../workitems/WorkItemForm";
import { WORK_ITEM_STATUSES } from "../../types/domain";
import type { User, WorkItem, WorkItemStatus } from "../../types/domain";

function DraggableCard({
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
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: item.id,
    data: { item },
  });

  const style = transform ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)` } : undefined;

  return (
    <div
      ref={setNodeRef}
      style={{ ...style, touchAction: "none" }}
      className={isDragging ? "dragging" : undefined}
      onClick={onOpen}
      {...listeners}
      {...attributes}
    >
      <WorkItemCard item={item} assignee={assignee} subtaskProgress={subtaskProgress} />
    </div>
  );
}

function DroppableColumn({ status, children }: { status: WorkItemStatus; children: ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: status });
  return (
    <div
      ref={setNodeRef}
      className="board-column-body"
      style={{
        background: isOver ? "var(--color-accent-tint)" : undefined,
        borderRadius: "var(--radius-md)",
        transition: "background 0.15s ease",
      }}
    >
      {children}
    </div>
  );
}

export function Board({
  itemsByStatus,
  assigneeOf,
  subtaskProgressOf,
  onOpenItem,
  onStatusChange,
}: {
  itemsByStatus: Record<WorkItemStatus, WorkItem[]>;
  assigneeOf: (item: WorkItem) => User | undefined;
  subtaskProgressOf: (item: WorkItem) => { done: number; total: number } | undefined;
  onOpenItem: (item: WorkItem) => void;
  onStatusChange: (item: WorkItem, newStatus: WorkItemStatus) => void;
}) {
  const [activeItem, setActiveItem] = useState<WorkItem | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 8 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 200, tolerance: 8 } }),
  );

  function handleDragStart(event: DragStartEvent) {
    setActiveItem((event.active.data.current?.item as WorkItem | undefined) ?? null);
  }

  function handleDragEnd(event: DragEndEvent) {
    setActiveItem(null);
    const { active, over } = event;
    if (!over) return;
    const item = active.data.current?.item as WorkItem | undefined;
    const newStatus = over.id as WorkItemStatus;
    if (!item || item.status === newStatus) return;
    onStatusChange(item, newStatus);
  }

  return (
    <DndContext sensors={sensors} onDragStart={handleDragStart} onDragEnd={handleDragEnd}>
      <div className="board">
        {WORK_ITEM_STATUSES.map((status) => (
          <div key={status} className="board-column">
            <div className="board-column-header">
              <span>{statusLabel(status)}</span>
              <span className="board-column-count">{itemsByStatus[status].length}</span>
            </div>
            <DroppableColumn status={status}>
              {itemsByStatus[status].length === 0 && (
                <div className="muted" style={{ padding: "var(--space-3) var(--space-2)", fontSize: "var(--font-size-sm)" }}>
                  Drop cards here
                </div>
              )}
              {itemsByStatus[status].map((item) => (
                <DraggableCard
                  key={item.id}
                  item={item}
                  assignee={assigneeOf(item)}
                  subtaskProgress={subtaskProgressOf(item)}
                  onOpen={() => onOpenItem(item)}
                />
              ))}
            </DroppableColumn>
          </div>
        ))}
      </div>
      <DragOverlay>
        {activeItem && <WorkItemCard item={activeItem} assignee={assigneeOf(activeItem)} subtaskProgress={subtaskProgressOf(activeItem)} />}
      </DragOverlay>
    </DndContext>
  );
}
