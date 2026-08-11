import { seedProjectMembers, seedProjects, seedWorkItems } from "./seedData";
import { delay, mutate, nextId, MockApiError } from "./mockClient";
import type { Guid, Project, WorkItem } from "../types/domain";

export interface ProjectBoard {
  project: Project;
  workItems: WorkItem[];
}

export async function fetchProjects(): Promise<Project[]> {
  return delay([...seedProjects]);
}

export async function fetchProjectBoard(projectId: Guid): Promise<ProjectBoard> {
  const project = seedProjects.find((p) => p.id === projectId);
  if (!project) {
    throw new MockApiError(`Project ${projectId} not found`, 404);
  }
  return delay({
    project: { ...project },
    workItems: seedWorkItems.filter((w) => w.projectId === projectId).map((w) => ({ ...w })),
  });
}

export interface CreateProjectInput {
  name: string;
  description: string | null;
  createdByUserId: Guid;
}

export async function createProject(input: CreateProjectInput): Promise<Project> {
  return mutate(() => {
    const project: Project = {
      id: nextId("p"),
      name: input.name,
      description: input.description,
      createdByUserId: input.createdByUserId,
      createdAt: new Date().toISOString(),
    };
    seedProjects.push(project);
    seedProjectMembers.push({
      projectId: project.id,
      userId: project.createdByUserId,
      role: "Owner",
      joinedAt: project.createdAt,
    });
    return { ...project };
  });
}
