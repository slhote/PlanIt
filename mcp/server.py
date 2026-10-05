# server.py
import logging

import httpx
from mcp.server.mcpserver import MCPServer

from planit_client import authorized_request

# Create a logger that does not write to stdout
logger = logging.getLogger(__name__)

# Create the server instance with a name
mcp = MCPServer("planit-mcp")


@mcp.tool()
async def get_open_workitems(projectName: str) -> str:
    """Get work items that are not already completed from a specific project

    Args:
        projectName: Name of the project that contains the work items to retrieve
    """
    try:
        projects_response = await authorized_request("GET", "/projects")
        projects = projects_response.json()

        # Find project by name
        project = next((p for p in projects if p["name"].lower() == projectName.lower()), None)
        if not project:
            return f"Project '{projectName}' not found"

        # Get board (all work items for the project)
        board_response = await authorized_request("GET", f"/projects/{project['id']}")
        board = board_response.json()

        # Filter for open work items (not Completed)
        open_items = [
            item for item in board["workItems"]
            if item["status"] != "Completed"
        ]

        if not open_items:
            return f"No open work items in project '{projectName}'"

        # Format results
        result = [
            f"[{item['workItemType']}] {item['title']} "
            f"(Status: {item['status']}, Assignee: {item.get('assigneeId', 'Unassigned')})"
            for item in open_items
        ]

        return "\n".join(result)

    except httpx.HTTPError as e:
        return f"API Error: {str(e)}"
    except RuntimeError as e:
        return f"Configuration error: {str(e)}"


if __name__ == "__main__":
    logger.info("Starting mcp server...")
    mcp.run()
