import { apiFetch } from "./httpClient";
import type { Guid, Project, WorkItem } from "../types/domain";

export interface ProjectBoard {
  project: Project;
  workItems: WorkItem[];
}

export async function fetchProjects(): Promise<Project[]> {
  return apiFetch<Project[]>("/projects");
}

export async function fetchProjectBoard(projectId: Guid): Promise<ProjectBoard> {
  return apiFetch<ProjectBoard>(`/projects/${projectId}`);
}

export interface CreateProjectInput {
  name: string;
  description: string | null;
}

export async function createProject(input: CreateProjectInput): Promise<Project> {
  return apiFetch<Project>("/projects", {
    method: "POST",
    body: JSON.stringify(input),
  });
}
