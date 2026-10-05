# PlanIt MCP Server

MCP server exposing the PlanIt API. Registered for this repo in the root [`.mcp.json`](../.mcp.json).

## Setup

1. Install [`uv`](https://docs.astral.sh/uv/getting-started/installation/). It is the only prerequisite; Python and the virtual environment are managed for you.
2. Copy `.env.example` to `.env` in this folder and fill in the values (API URL and service account credentials). `.env` is gitignored.
3. Start the PlanIt API (`dotnet run --project PlanIt.Api` from the repo root).
4. Open the repo in Claude Code and approve the `planit` project MCP server when prompted.

On first launch, `uv run` creates `mcp/.venv` and installs the dependencies from `pyproject.toml` / `uv.lock`, so the first start takes a few seconds longer.

## Running manually

```bash
cd mcp
uv run python server.py
```
