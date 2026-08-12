namespace PlanIt.Api.Domain.Exceptions;

// Maps to 404 Not Found (planit-system-design-architecture.md §6).
public class TaskNotFoundException : Exception
{
    public TaskNotFoundException(string message) : base(message)
    {
    }
}
