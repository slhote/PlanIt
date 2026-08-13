import { useQuery } from "@tanstack/react-query";
import { fetchProjects, fetchProjectBoard } from "../api/projects";
import { fetchFeature, fetchWorkItem } from "../api/workItems";
import { fetchProjectMembers } from "../api/projectMembers";
import { searchUsers } from "../api/users";
import type { Guid } from "../types/domain";

export function useProjectsQuery() {
  return useQuery({ queryKey: ["projects"], queryFn: fetchProjects });
}

export function useProjectBoardQuery(projectId: Guid | undefined) {
  return useQuery({
    queryKey: ["project", projectId],
    queryFn: () => fetchProjectBoard(projectId as Guid),
    enabled: !!projectId,
    retry: false,
  });
}

export function useFeatureQuery(projectId: Guid | undefined, featureId: Guid | undefined) {
  return useQuery({
    queryKey: ["feature", featureId],
    queryFn: () => fetchFeature(projectId as Guid, featureId as Guid),
    enabled: !!projectId && !!featureId,
  });
}

export function useWorkItemQuery(projectId: Guid | undefined, workItemId: Guid | undefined) {
  return useQuery({
    queryKey: ["workItem", workItemId],
    queryFn: () => fetchWorkItem(projectId as Guid, workItemId as Guid),
    enabled: !!projectId && !!workItemId,
  });
}

export function useProjectMembersQuery(projectId: Guid | undefined) {
  return useQuery({
    queryKey: ["projectMembers", projectId],
    queryFn: () => fetchProjectMembers(projectId as Guid),
    enabled: !!projectId,
  });
}

export function useUserSearchQuery(query: string) {
  return useQuery({
    queryKey: ["userSearch", query],
    queryFn: () => searchUsers(query),
    enabled: query.trim().length > 0,
  });
}
