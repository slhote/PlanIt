# PlanIt.EmbeddingService

FastAPI + `sentence-transformers` microservice -- the Python generation source for Similar Tasks semantic embeddings.

Called by `PlanIt.Api`'s `PythonEmbeddingGenerator` over internal HTTP. Not required for local
dev unless `SimilarWorkItems:EmbeddingSource` is `"Python"` -- the ONNX path works standalone.

## Run via Docker Compose

Started automatically as part of the `full-stack` profile:

```bash
docker compose --profile full-stack up -d embedding-service
```

## Run standalone (without Docker)

```bash
cd PlanIt.EmbeddingService
python -m venv .venv
.venv\Scripts\activate      # Windows; use `source .venv/bin/activate` on macOS/Linux
pip install -r requirements.txt
uvicorn main:app --reload --port 8000
```

`GET /health` confirms the model loaded. `POST /embed` with `{"text": "..."}` returns
`{"vector": [...], "dimensions": 768}`.
