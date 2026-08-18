"""Similar Tasks semantic embeddings -- Python generation source (Option B).

Separate process from PlanIt.Api, called over internal HTTP by PythonEmbeddingGenerator.
Deliberately a different model from the ONNX path (all-mpnet-base-v2, 768-dim vs.
all-MiniLM-L6-v2, 384-dim) -- the point of running both sources is comparing distinct
approaches, not two copies of the same model through different runtimes.
See .claude/docs/plans/planit-similar-tasks-semantic-embeddings.md.
"""

from fastapi import FastAPI
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer

MODEL_NAME = "sentence-transformers/all-mpnet-base-v2"

app = FastAPI(title="PlanIt Embedding Service")
model = SentenceTransformer(MODEL_NAME)


class EmbedRequest(BaseModel):
    text: str


class EmbedResponse(BaseModel):
    vector: list[float]
    dimensions: int


@app.get("/health")
def health():
    return {"status": "healthy", "model": MODEL_NAME}


@app.post("/embed", response_model=EmbedResponse)
def embed(request: EmbedRequest):
    # normalize_embeddings=True matches the ONNX path's L2-normalization, so cosine
    # similarity on either side is a plain dot product.
    vector = model.encode(request.text, normalize_embeddings=True).tolist()
    return EmbedResponse(vector=vector, dimensions=len(vector))
