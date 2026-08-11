import { useParams } from "react-router";
import { WorkItemDetailContent } from "./WorkItemDetailContent";

export function WorkItemDetailPage({ kind }: { kind: "feature" | "task" }) {
  const params = useParams<{ projectId: string; featureId?: string; taskId?: string }>();
  const projectId = params.projectId as string;
  const workItemId = (kind === "feature" ? params.featureId : params.taskId) as string;
  const parentFeatureId = kind === "task" ? params.featureId : undefined;

  return (
    <div className="page">
      <WorkItemDetailContent
        projectId={projectId}
        workItemId={workItemId}
        kind={kind}
        parentFeatureId={parentFeatureId}
      />
    </div>
  );
}
