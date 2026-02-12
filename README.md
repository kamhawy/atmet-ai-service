# ATMET AI Service

A comprehensive .NET 10 Web API that encapsulates Azure AI Foundry SDK capabilities, providing unified REST endpoints for frontend SPA applications. This service replaces direct Azure AI Foundry SDK usage in client applications (e.g., the **atmetai-sigmapaints-demo** React SPA), enabling centralized configuration, managed identity authentication to Azure, and consistent API contracts.

## 🚀 Features

- **Agents Management**: Create, manage, and execute AI agents with full conversation support (threads, messages, runs, file uploads)
- **Deployments**: Enumerate and manage AI model deployments (GPT-4, GPT-4o, etc.)
- **Connections**: List and inspect Azure resource connections (OpenAI, AI Search, storage)
- **Datasets**: Upload, version, and manage datasets for RAG and training
- **Indexes**: Create and manage Azure AI Search indexes for RAG scenarios
- **Chat Completions**: Azure OpenAI chat completions with sync and streaming support
- **Managed Identity**: Secure, keyless authentication to Azure services
- **API Key Auth**: Simple `X-Api-Key` header authentication for SPA clients
- **Health Checks**: Comprehensive health monitoring (`/health`, `/health/ready`, `/health/live`)
- **Observability**: Application Insights integration with structured logging
- **Performance**: Output caching, response compression, and connection pooling
- **Security**: API key authentication, CORS, rate limiting, security headers

## 📋 Prerequisites

- .NET 10 SDK
- Azure Subscription
- Azure AI Foundry Project
- Visual Studio 2024 or VS Code

## 🏗️ Architecture

```text
┌─────────────────────────────────────────┐
│         API Layer (Minimal APIs)        │
│  AgentsEndpoints, ChatEndpoints, etc.   │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│         Service Layer                    │
│  IAgentService, IChatService, etc.       │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│    Infrastructure Layer                  │
│  AzureAIClientFactory, Repositories      │
└─────────────────────────────────────────┘
                    │
┌─────────────────────────────────────────┐
│    Azure AI Foundry Project              │
│  (Managed Identity Authentication)       │
└─────────────────────────────────────────┘
```

## 🛠️ Setup Instructions

### 1. Clone and Build

```bash
git clone <repository-url>
cd ATMET.AI.Service
dotnet restore
dotnet build
```

### 2. Configure Azure Resources

#### Create Azure AI Foundry Project

```bash
# Login to Azure
az login

# Create resource group
az group create --name rg-atmet-ai --location eastus

# Create AI Foundry project (use Azure Portal for full setup)
# Navigate to https://ai.azure.com
```

#### Enable Managed Identity

For Azure App Service:

```bash
# Enable system-assigned managed identity
az webapp identity assign \
    --name your-app-service-name \
    --resource-group rg-atmet-ai

# Or create user-assigned identity
az identity create \
    --name atmet-ai-identity \
    --resource-group rg-atmet-ai

# Assign to App Service
az webapp identity assign \
    --name your-app-service-name \
    --resource-group rg-atmet-ai \
    --identities /subscriptions/{sub-id}/resourcegroups/rg-atmet-ai/providers/Microsoft.ManagedIdentity/userAssignedIdentities/atmet-ai-identity
```

#### Grant Permissions

```bash
# Get the managed identity object ID
IDENTITY_ID=$(az webapp identity show \
    --name your-app-service-name \
    --resource-group rg-atmet-ai \
    --query principalId -o tsv)

# Assign Cognitive Services User role
az role assignment create \
    --role "Cognitive Services User" \
    --assignee-object-id $IDENTITY_ID \
    --scope /subscriptions/{sub-id}/resourceGroups/rg-atmet-ai

# Assign Search Index Data Contributor (if using AI Search)
az role assignment create \
    --role "Search Index Data Contributor" \
    --assignee-object-id $IDENTITY_ID \
    --scope /subscriptions/{sub-id}/resourceGroups/rg-atmet-ai

# Assign Storage Blob Data Contributor (for datasets)
az role assignment create \
    --role "Storage Blob Data Contributor" \
    --assignee-object-id $IDENTITY_ID \
    --scope /subscriptions/{sub-id}/resourceGroups/rg-atmet-ai
```

### 3. Configure Application Settings

Update `appsettings.json`:

```json
{
  "AzureAI": {
    "ProjectEndpoint": "https://your-resource.services.ai.azure.com/api/projects/your-project-id",
    "ManagedIdentityClientId": null,
    "DefaultModelDeployment": "gpt-4o"
  },
  "ApiKeys": {
    "HeaderName": "X-Api-Key",
    "Keys": ["your-api-key-for-spa-clients"]
  },
  "ApplicationInsights": {
    "ConnectionString": "your-app-insights-connection-string"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://your-spa-domain.com",
      "http://localhost:3000",
      "http://localhost:4200"
    ]
  },
  "RateLimiting": {
    "PermitLimit": 100,
    "Window": "00:01:00"
  }
}
```

For Azure App Service, use Application Settings instead:

```bash
az webapp config appsettings set \
    --name your-app-service-name \
    --resource-group rg-atmet-ai \
    --settings \
    AzureAI__ProjectEndpoint="https://..." \
    ApiKeys__Keys__0="your-api-key" \
    Cors__AllowedOrigins__0="https://your-spa.com" \
    ApplicationInsights__ConnectionString="..."
```

### 4. Local Development

```bash
# Run locally
cd src/ATMET.AI.Api
dotnet run

# API docs: /scalar (Scalar) or /swagger (Swagger UI)
# Example: https://localhost:5001/scalar
```

For local development, `DefaultAzureCredential` authenticates to Azure:

1. Azure CLI credentials (`az login`)
2. Visual Studio / VS Code credentials
3. Managed Identity (when deployed)

### 5. Deploy to Azure

```bash
# Publish the application
dotnet publish -c Release -o ./publish

# Deploy to App Service
az webapp deploy \
    --resource-group rg-atmet-ai \
    --name your-app-service-name \
    --src-path ./publish.zip \
    --type zip
```

Or use GitHub Actions / Azure DevOps pipelines (see `.github/workflows` or `azure-pipelines.yml`).

## 📖 API Documentation

### Base URL

```text
https://your-app-service.azurewebsites.net/api/v1
```

Interactive documentation: **/scalar** (Scalar UI) or **/swagger** (Swagger UI).

### Authentication

All endpoints require an API key in the `X-Api-Key` header:

```http
X-Api-Key: your-api-key
```

Configure valid keys in `appsettings.json` under `ApiKeys.Keys`.

**Example request:**

```bash
curl -X GET "https://your-api.azurewebsites.net/api/v1/agents" \
  -H "X-Api-Key: your-api-key"
```

### Endpoints Overview

#### Agents

| Method   | Path                                             | Description                |
| -------- | ------------------------------------------------ | -------------------------- |
| `POST`   | `/agents`                                        | Create agent               |
| `GET`    | `/agents`                                        | List agents (limit, order) |
| `GET`    | `/agents/{agentId}`                              | Get agent                  |
| `PUT`    | `/agents/{agentId}`                              | Update agent               |
| `DELETE` | `/agents/{agentId}`                              | Delete agent               |
| `POST`   | `/agents/{agentId}/threads`                      | Create thread              |
| `GET`    | `/agents/threads/{threadId}`                     | Get thread                 |
| `DELETE` | `/agents/threads/{threadId}`                     | Delete thread              |
| `POST`   | `/agents/threads/{threadId}/messages`            | Add message                |
| `GET`    | `/agents/threads/{threadId}/messages`            | Get messages               |
| `POST`   | `/agents/threads/{threadId}/runs`                | Create/execute run         |
| `GET`    | `/agents/threads/{threadId}/runs/{runId}`        | Get run status             |
| `POST`   | `/agents/threads/{threadId}/runs/{runId}/cancel` | Cancel run                 |
| `POST`   | `/agents/files`                                  | Upload file for agent use  |
| `GET`    | `/agents/files/{fileId}`                         | Get file metadata          |
| `DELETE` | `/agents/files/{fileId}`                         | Delete file                |

#### Deployments

| Method | Path                            | Description                                        |
| ------ | ------------------------------- | -------------------------------------------------- |
| `GET`  | `/deployments`                  | List AI models (modelPublisher, modelType filters) |
| `GET`  | `/deployments/{deploymentName}` | Get model details                                  |

#### Connections

| Method | Path                            | Description                              |
| ------ | ------------------------------- | ---------------------------------------- |
| `GET`  | `/connections`                  | List connections (connectionType filter) |
| `GET`  | `/connections/default`          | Get default project connection           |
| `GET`  | `/connections/{connectionName}` | Get connection details                   |

#### Datasets (RAG / Training)

| Method   | Path                                  | Description                     |
| -------- | ------------------------------------- | ------------------------------- |
| `POST`   | `/datasets/upload/file`               | Upload single file (multipart)  |
| `POST`   | `/datasets/upload/folder`             | Upload multiple files           |
| `GET`    | `/datasets`                           | List datasets (latest versions) |
| `GET`    | `/datasets/{name}/versions`           | List dataset versions           |
| `GET`    | `/datasets/{name}/versions/{version}` | Get dataset                     |
| `DELETE` | `/datasets/{name}/versions/{version}` | Delete dataset                  |

#### Indexes (RAG / Azure AI Search)

| Method   | Path                                 | Description            |
| -------- | ------------------------------------ | ---------------------- |
| `POST`   | `/indexes`                           | Create or update index |
| `GET`    | `/indexes`                           | List indexes           |
| `GET`    | `/indexes/{name}/versions`           | List index versions    |
| `GET`    | `/indexes/{name}/versions/{version}` | Get index              |
| `DELETE` | `/indexes/{name}/versions/{version}` | Delete index           |

#### Chat

| Method | Path                       | Description                |
| ------ | -------------------------- | -------------------------- |
| `POST` | `/chat/completions`        | Chat completion            |
| `POST` | `/chat/completions/stream` | Streaming completion (SSE) |

Full API reference: `docs/API-REFERENCE.md`

## 🌐 SPA Integration

The **atmetai-sigmapaints-demo** React SPA uses this API as a replacement for direct Azure AI Foundry SDK usage. Configure the SPA with:

```env
VITE_ATMET_AI_API_URL=https://your-atmet-ai-service.azurewebsites.net
VITE_ATMET_AI_API_KEY=your-api-key
```

Features wired to this API:

- **Knowledge Base (RAG)**: Datasets, indexes, upload, chat completions for test queries
- **Agents Settings**: (planned) List/sync agents, deployments, connections
- **Agents Monitor**: (planned) Execution logs, runs

Add the SPA origin to `Cors.AllowedOrigins` in `appsettings.json`.

## 🧪 Testing

```bash
# Run unit tests
dotnet test

# Run integration tests (requires Azure setup)
dotnet test --filter Category=Integration

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 🔍 Monitoring

### Health Checks

- `/health` - Overall health status
- `/health/ready` - Readiness probe
- `/health/live` - Liveness probe

### Application Insights

Automatically tracks:

- HTTP requests/responses
- Dependency calls (Azure AI SDK)
- Exceptions and errors
- Custom metrics
- Performance counters

### Logging

Structured logging with Serilog:

- Console output (development)
- Application Insights (production)
- Request/response correlation

## 🔒 Security

- **Authentication**: API key via `X-Api-Key` header (for SPA clients)
- **Azure Services**: Managed Identity for keyless auth to Azure AI, Storage, Search
- **CORS**: Configurable allowed origins (SPA domains, localhost)
- **HTTPS**: Enforced in production
- **Rate Limiting**: Fixed window (100 req/min by default)
- **Security Headers**: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection

## ⚡ Performance

- **Output Caching**: Cached responses for deployments, connections, indexes (configurable TTL)
- **Response Compression**: Gzip compression
- **Connection Pooling**: Singleton Azure AI clients via `AzureAIClientFactory`
- **Async/Await**: Non-blocking operations throughout
- **Minimal APIs**: Lightweight endpoint registration

## 🤝 Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## 📝 License

This project is licensed under the MIT License.

## 🆘 Support

For issues and questions:

- GitHub Issues: [Project Issues](https://github.com/your-org/atmet-ai-service/issues)
- Documentation: [Wiki](https://github.com/your-org/atmet-ai-service/wiki)
- Email: <ai-team@atmet.ai>

## 🗺️ Roadmap

- [ ] Migrate Agents Settings & Monitor from direct Foundry SDK to this API
- [ ] Batch processing support
- [ ] WebSocket support for real-time streaming
- [ ] Multi-tenancy / per-entity configuration
- [ ] Custom tool integrations
- [ ] OpenTelemetry integration

## 📚 Additional Resources

- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
- [Azure AI Projects SDK](https://learn.microsoft.com/dotnet/api/overview/azure/ai.projects-readme)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [.NET 10 Documentation](https://learn.microsoft.com/dotnet/)
