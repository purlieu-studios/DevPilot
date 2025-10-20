# RAG (Retrieval Augmented Generation)

**Status**: ✅ Implemented (Feature flag: `--enable-rag`)

DevPilot includes an optional Retrieval Augmented Generation (RAG) system that enhances agents with semantic search over workspace files. When enabled, agents receive relevant code snippets, documentation, and examples as context, improving the quality and accuracy of generated code.

## Architecture Overview

**Components**:

1. **Document Chunker** (`DocumentChunker.cs`)
   - Splits large documents into overlapping chunks (default: 512 tokens, 50 token overlap)
   - Preserves context across chunk boundaries
   - Smart boundary detection: prefers paragraphs → lines → sentences → whitespace

2. **Embedding Service** (`OllamaEmbeddingService.cs`)
   - Generates vector embeddings using Ollama's `mxbai-embed-large` model
   - 1024-dimensional embeddings (59.25% MTEB retrieval accuracy)
   - Local inference (no external API calls)
   - Uses Microsoft.Extensions.AI abstractions

3. **Vector Store** (`SqliteVectorStore.cs`)
   - SQLite-based persistent storage
   - Cosine similarity search using `System.Numerics.Tensors.TensorPrimitives`
   - Metadata filtering (workspace ID, file path, file extension)
   - Per-workspace databases (`.devpilot/rag/{workspace-id}.db`)

4. **RAG Service** (`RagService.cs`)
   - Orchestrates indexing and retrieval
   - File discovery with configurable include/exclude patterns
   - Contextual output formatting (prioritizes CLAUDE.md)
   - Top-K retrieval with token budget management

**Workflow**:

```
User Request
    ↓
WorkspaceManager copies files to isolated workspace
    ↓
RagService.IndexWorkspaceAsync()
    ├─ Discover files (.cs, .csproj, .md, .json, .yaml)
    ├─ Chunk documents (512 tokens, 50 overlap)
    ├─ Generate embeddings (Ollama: mxbai-embed-large)
    └─ Store in SQLite vector database
    ↓
RagService.QueryAsync(userRequest)
    ├─ Embed query using Ollama
    ├─ Search vector store (top-5 by cosine similarity)
    └─ Filter by workspace ID
    ↓
RagService.FormatContext()
    ├─ Prioritize CLAUDE.md chunks
    ├─ Format with file path + chunk index + relevance score
    └─ Limit to 8000 tokens (approx)
    ↓
Pipeline injects RAG context into agent prompts
    ↓
Agents generate code with relevant workspace context
```

## Setup

### 1. Install Ollama

**Windows** (via winget):
```powershell
winget install Ollama.Ollama
```

**macOS** (via Homebrew):
```bash
brew install ollama
```

**Linux**:
```bash
curl -fsSL https://ollama.com/install.sh | sh
```

Verify installation:
```bash
ollama --version
```

### 2. Pull the Embedding Model

```bash
ollama pull mxbai-embed-large
```

This downloads the model (~669 MB). Verify it's available:

```bash
ollama list
```

Expected output:
```
NAME                    ID              SIZE    MODIFIED
mxbai-embed-large:latest   xyz123...       669 MB  ...
```

### 3. Start Ollama Service

Ollama runs as a background service by default. If needed, start it manually:

```bash
ollama serve
```

Expected: `Ollama is running on http://localhost:11434`

## Usage

Enable RAG with the `--enable-rag` flag:

```bash
cd C:\Projects\MyApp
devpilot --enable-rag "Add authentication to User class"
```

**What Happens**:
1. DevPilot indexes your workspace files (`.cs`, `.csproj`, `.md`, etc.)
2. Embeds your request: "Add authentication to User class"
3. Retrieves top-5 most relevant code chunks
4. Injects context into agent prompts
5. Agents generate code aware of your existing patterns

**Example RAG Context Injected**:

```markdown
# Relevant Context from Workspace

## CLAUDE.md (chunk 0, relevance: 0.892)
This is an e-commerce application. User model has email/password authentication.
Payments are processed via Stripe. Follow PCI compliance guidelines...

---

## src/Models/User.cs (chunk 0, relevance: 0.765)
public class User
{
    public int Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
}

---

## src/Services/AuthService.cs (chunk 1, relevance: 0.734)
public class AuthService
{
    public async Task<bool> AuthenticateAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        return BCrypt.Verify(password, user.PasswordHash);
    }
}

---
```

Agents now know:
- Your project uses BCrypt for password hashing
- User model already has Email/PasswordHash properties
- You have an AuthService pattern they should follow

## Configuration

Customize RAG behavior via `RAGOptions` (in `src/DevPilot.RAG/RAGOptions.cs`):

```csharp
public sealed class RAGOptions
{
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "mxbai-embed-large";
    public int EmbeddingDimension { get; set; } = 1024;

    // Chunk configuration
    public int ChunkSize { get; set; } = 512;           // tokens
    public int OverlapSize { get; set; } = 50;          // tokens

    // Retrieval configuration
    public int TopK { get; set; } = 5;                  // top results
    public int MaxContextTokens { get; set; } = 8000;   // total context budget

    // File filtering
    public string[] IncludeExtensions { get; set; } = new[]
    {
        ".cs", ".csproj", ".sln",
        ".md", ".txt",
        ".json", ".yaml", ".yml",
        ".xml", ".config"
    };

    public string[] ExcludePatterns { get; set; } = new[]
    {
        "**/bin/**", "**/obj/**", "**/.git/**",
        "**/.vs/**", "**/node_modules/**", "**/packages/**"
    };

    public string DatabasePath { get; set; } = ".devpilot/rag/{workspace-id}.db";
}
```

## Troubleshooting

### "Failed to connect to Ollama"

**Symptom**: `RAG disabled: Failed to connect to Ollama. Ensure Ollama is running and the model is pulled.`

**Solution**:
```bash
# Check if Ollama is running
curl http://localhost:11434

# If not running, start it
ollama serve

# Verify model is available
ollama list | grep mxbai-embed-large

# If not found, pull it
ollama pull mxbai-embed-large
```

### "Expected embedding dimension 1024, but got 768"

**Symptom**: Pipeline fails with dimension mismatch error.

**Cause**: Wrong embedding model used (e.g., `all-minilm-l6-v2` has 384 dimensions).

**Solution**: Ensure you're using the correct model:
```bash
ollama pull mxbai-embed-large
```

### RAG Disabled by Default

**Symptom**: Agents don't seem to have workspace context.

**Solution**: RAG is opt-in. Add `--enable-rag` flag:
```bash
devpilot --enable-rag "your request here"
```

### Slow Indexing

**Symptom**: `IndexWorkspaceAsync` takes several minutes on large repos.

**Explanation**:
- Embedding generation is CPU-intensive
- 1000 chunks ≈ 2-5 minutes on typical hardware
- SQLite writes are fast; Ollama inference is the bottleneck

**Optimization**: Exclude unnecessary directories in `ExcludePatterns`:
```csharp
ExcludePatterns = new[]
{
    "**/bin/**", "**/obj/**",
    "**/TestData/**",        // Add this
    "**/LargeAssets/**"      // Add this
};
```

### Low Relevance Scores

**Symptom**: Retrieved chunks seem unrelated (scores < 0.5).

**Cause**: Query and documents use different terminology.

**Solution**:
1. **Improve Query Phrasing**: Be specific
   - ❌ "Add auth" (too vague)
   - ✅ "Add JWT authentication with Bearer token validation"
2. **Enhance CLAUDE.md**: Add domain-specific terminology
   ```markdown
   ## Authentication
   We use JWT tokens (JSON Web Tokens) with Bearer scheme.
   Tokens are validated using HS256 algorithm with secret key...
   ```

## Implementation Status

**✅ Implemented**:
- Document chunking with smart boundary detection
- Ollama embedding service (mxbai-embed-large)
- SQLite vector store with cosine similarity search
- RAG service orchestration (indexing + retrieval)
- Pipeline integration (context injection into all 5 stages)
- Graceful degradation if Ollama unavailable
- Unit tests (28 tests covering DocumentChunker, SqliteVectorStore, OllamaEmbeddingService)

**📝 Future Enhancements**:
- [ ] Alternative embedding models (OpenAI ada-002, Cohere)
- [ ] Hybrid search (keyword + vector)
- [ ] Incremental indexing (only re-index changed files)
- [ ] Chunk-level metadata (author, last modified, code complexity)
- [ ] Multi-workspace search (query across multiple projects)
- [ ] RAG quality metrics dashboard (precision, recall, MRR)

## Testing

Run RAG tests:

```bash
# All RAG tests (DocumentChunker, SqliteVectorStore, OllamaEmbeddingService)
dotnet test tests/DevPilot.RAG.Tests/

# Specific test class
dotnet test --filter "FullyQualifiedName~SqliteVectorStoreTests"

# Integration tests require Ollama running
# (currently skipped; manual testing recommended)
```

**Test Coverage**:
- DocumentChunker: 13 tests (chunk sizes, overlap, edge cases)
- SqliteVectorStore: 9 tests (CRUD, search, filtering, cosine similarity)
- OllamaEmbeddingService: 6 tests (constructor, validation)
- **Total**: 28 tests, all passing ✅

## Related Documentation

- [CLAUDE.md](../CLAUDE.md) - Main project instructions
- [PIPELINE.md](./PIPELINE.md) - Pipeline architecture
- [ARCHITECTURE.md](./ARCHITECTURE.md) - MASAI system design
