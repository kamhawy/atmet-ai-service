# ATMET AI Service - Architecture Document

## Overview

A comprehensive .NET 10 Web API that encapsulates Azure AI Foundry SDK capabilities, providing unified endpoints for frontend SPA applications.

## Technology Stack

- **.NET 10** (Latest features)
- **Azure.AI.Projects 1.1.0** (Main SDK)
- **Azure.AI.Agents.Persistent 1.2.0-beta.2** (Agents)
- **Azure.Identity** (Managed Identity Authentication)
- **Minimal APIs** (Performance-optimized routing)

## Core Capabilities

### 1. Agents Management

- Create, update, delete, and list agents
- Thread management (conversations)
- Message operations
- Run execution and monitoring
- File operations for agents
- Tool integration (file search, code interpreter, function calling)

### 2. Deployments (AI Models)

- List all deployed models
- Get deployment details
- Filter by publisher/model type

### 3. Connections

- List Azure resource connections
- Get connection details with/without credentials
- Filter by connection type
- Retrieve default connections

### 4. Datasets

- Upload files and folders
- Create and version datasets
- List dataset versions
- Get dataset credentials
- Delete datasets

### 5. Indexes (Search)

- Create and update indexes
- List all indexes and versions
- Get index details
- Delete indexes
- Azure AI Search integration

### 6. Chat & Completions

- Azure OpenAI chat completions
- Streaming support
- Function calling
- Multi-modal support

## Architecture Layers

```
┌─────────────────────────────────────────┐
│         API Layer (Controllers)         │
│  Minimal APIs / Route Handlers          │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│         Service Layer                    │
│  Business Logic & Orchestration          │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│         Repository Layer                 │
│  Azure AI SDK Wrappers                   │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│    Azure AI Foundry Project              │
│  (Accessed via Managed Identity)         │
└─────────────────────────────────────────┘
```

## Project Structure

```
ATMET.AI.Service/
├── src/
│   ├── ATMET.AI.Api/                    # Web API Project
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Endpoints/                   # Minimal API Endpoints
│   │   │   ├── AgentsEndpoints.cs
│   │   │   ├── DeploymentsEndpoints.cs
│   │   │   ├── ConnectionsEndpoints.cs
│   │   │   ├── DatasetsEndpoints.cs
│   │   │   ├── IndexesEndpoints.cs
│   │   │   └── ChatEndpoints.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   └── RequestLoggingMiddleware.cs
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── ATMET.AI.Core/                   # Core Business Logic
│   │   ├── Services/
│   │   │   ├── IAgentService.cs
│   │   │   ├── AgentService.cs
│   │   │   ├── IDeploymentService.cs
│   │   │   ├── DeploymentService.cs
│   │   │   ├── IConnectionService.cs
│   │   │   ├── ConnectionService.cs
│   │   │   ├── IDatasetService.cs
│   │   │   ├── DatasetService.cs
│   │   │   ├── IIndexService.cs
│   │   │   ├── IndexService.cs
│   │   │   ├── IChatService.cs
│   │   │   └── ChatService.cs
│   │   ├── Models/
│   │   │   ├── Requests/
│   │   │   └── Responses/
│   │   └── Exceptions/
│   │
│   └── ATMET.AI.Infrastructure/         # Infrastructure Layer
│       ├── Clients/
│       │   └── AzureAIClientFactory.cs
│       ├── Configuration/
│       │   └── AzureAIOptions.cs
│       └── Extensions/
│           └── AzureAIExtensions.cs
│
├── tests/
│   ├── ATMET.AI.Api.Tests/
│   ├── ATMET.AI.Core.Tests/
│   └── ATMET.AI.Infrastructure.Tests/
│
└── docs/
    ├── API.md
    └── DEPLOYMENT.md
```

## API Endpoint Design

### Base Route: `/api/v1`

#### Agents

- `POST /agents` - Create agent
- `GET /agents` - List all agents
- `GET /agents/{agentId}` - Get agent details
- `PUT /agents/{agentId}` - Update agent
- `DELETE /agents/{agentId}` - Delete agent
- `POST /agents/{agentId}/threads` - Create thread
- `GET /agents/{agentId}/threads` - List threads
- `POST /threads/{threadId}/messages` - Add message
- `GET /threads/{threadId}/messages` - Get messages
- `POST /threads/{threadId}/runs` - Create run
- `GET /threads/{threadId}/runs/{runId}` - Get run status

#### Deployments

- `GET /deployments` - List deployments
- `GET /deployments/{deploymentName}` - Get deployment details

#### Connections

- `GET /connections` - List connections
- `GET /connections/{connectionName}` - Get connection details
- `GET /connections/default` - Get default connection

#### Datasets

- `POST /datasets` - Create/upload dataset
- `GET /datasets` - List datasets
- `GET /datasets/{name}/versions/{version}` - Get dataset
- `DELETE /datasets/{name}/versions/{version}` - Delete dataset
- `GET /datasets/{name}/versions` - List versions

#### Indexes

- `POST /indexes` - Create index
- `GET /indexes` - List indexes
- `GET /indexes/{name}/versions/{version}` - Get index
- `PUT /indexes/{name}/versions/{version}` - Update index
- `DELETE /indexes/{name}/versions/{version}` - Delete index

#### Chat

- `POST /chat/completions` - Chat completion
- `POST /chat/completions/stream` - Streaming completion

## Security & Authentication

### Managed Identity Configuration

- System-assigned or User-assigned Managed Identity
- Azure RBAC roles required:
  - `Cognitive Services User`
  - `Search Index Data Contributor`
  - `Storage Blob Data Contributor`

### API Security

- Azure AD Bearer Token authentication for API consumers
- API Key alternative for simple scenarios
- CORS configuration for SPA applications

## Performance Optimization

1. **Response Caching**
   - Cache deployments list (rarely changes)
   - Cache connection information
   - Configurable TTL

2. **Connection Pooling**
   - Singleton AIProjectClient
   - HttpClient best practices

3. **Async/Await**
   - All operations fully asynchronous
   - CancellationToken support

4. **Output Caching**
   - Use .NET 10 output caching middleware

## Observability

### Logging

- Structured logging with `ILogger`
- Azure Application Insights integration
- Request/Response logging

### Metrics

- Custom metrics for AI operations
- Performance counters
- Error rates

### Health Checks

- Azure AI endpoint connectivity
- Deployment availability
- Storage connectivity

## Error Handling

### Global Exception Handler

- Consistent error response format
- Proper HTTP status codes
- Error correlation IDs

### Retry Policies

- Polly for transient failures
- Exponential backoff
- Circuit breaker pattern

## Configuration

### appsettings.json Structure

```json
{
  "AzureAI": {
    "ProjectEndpoint": "https://{resource}.services.ai.azure.com/api/projects/{project}",
    "ManagedIdentityClientId": null,
    "EnableTelemetry": true,
    "DefaultModelDeployment": "gpt-4o"
  },
  "Caching": {
    "DeploymentsCacheDurationMinutes": 60,
    "ConnectionsCacheDurationMinutes": 30
  }
}
```

## Best Practices Implemented

1. **Dependency Injection** - All services registered in DI container
2. **Options Pattern** - Configuration via IOptions<T>
3. **Minimal APIs** - Performance-optimized routing
4. **Clean Architecture** - Separation of concerns
5. **SOLID Principles** - Single responsibility, dependency inversion
6. **Async Programming** - Non-blocking operations
7. **Error Handling** - Comprehensive exception management
8. **Logging** - Structured and contextual
9. **Testing** - Unit and integration tests
10. **Documentation** - OpenAPI/Swagger integration
