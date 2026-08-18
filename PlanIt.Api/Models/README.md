# ONNX embedding model

The Similar Tasks semantic-embeddings feature (`OnnxEmbeddingGenerator`) needs two local files,
not committed to the repo (large binaries — see `.gitignore`):

```
Models/all-MiniLM-L6-v2/model.onnx
Models/all-MiniLM-L6-v2/vocab.txt
```

These are the ONNX export of `sentence-transformers/all-MiniLM-L6-v2` and its WordPiece vocab.
Configured paths: `OnnxEmbedding:ModelPath` / `OnnxEmbedding:VocabPath` in `appsettings.json`.

Until these files are present, anything that resolves `OnnxEmbeddingGenerator` from DI will throw
on construction (fail-fast, by design — see `.claude/docs/plans/planit-similar-tasks-semantic-embeddings.md`).
Nothing resolves it yet as of step 2 of that plan, so `dotnet run` / `dev.ps1` work fine without
these files until the background worker (step 5) is wired up.

Source: `https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/tree/main/onnx`
(`model.onnx` + `vocab.txt`).
