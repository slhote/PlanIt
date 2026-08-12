using PlanIt.Api.Contracts.WorkItems;
using PlanIt.Api.Data;
using PlanIt.Api.Domain.Entities;
using PlanIt.Api.Domain.Exceptions;
using PlanIt.Api.Domain.Repositories;

namespace PlanIt.Api.Application;

// Read-only for now (planit-api-contracts-backend.md §8 step 1). CreateAsync/UpdateAsync/
// DeleteAsync, the hierarchy invariant, and the cascade strategies land in step 5.
public class WorkItemService(IWorkItemRepository workItemRepository, PlanItDbContext db)
{
    public async Task<WorkItemDto> GetByIdAsync(Guid id)
    {
        var workItem = await workItemRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"Work item {id} not found.");
        return WorkItemMapper.ToDto(workItem, db);
    }

    public async Task<FeatureDetailDto> GetFeatureDetailAsync(Guid id)
    {
        var feature = await workItemRepository.GetByIdAsync(id)
            ?? throw new TaskNotFoundException($"Work item {id} not found.");

        if (feature.WorkItemType != WorkItemType.Feature)
        {
            throw new TaskNotFoundException($"Work item {id} is not a Feature.");
        }

        var children = await workItemRepository.GetChildrenAsync(id);
        var childDtos = children.Select(c => WorkItemMapper.ToDto(c, db)).ToList();

        return new FeatureDetailDto(WorkItemMapper.ToDto(feature, db), childDtos);
    }
}
