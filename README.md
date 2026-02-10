# ATMET AI Service

A comprehensive .NET 10 Web API that encapsulates Azure AI Foundry SDK capabilities, providing unified REST endpoints for frontend SPA applications.

## 🚀 Features

- **Agents Management**: Create, manage, and execute AI agents with full conversation support
- **Deployments**: Enumerate and manage AI model deployments
- **Connections**: Manage Azure resource connections
- **Datasets**: Upload, version, and manage training datasets
- **Indexes**: Create and manage search indexes for RAG scenarios
- **Chat Completions**: Azure OpenAI chat completions with streaming support
- **Managed Identity**: Secure, keyless authentication to Azure services
- **Health Checks**: Comprehensive health monitoring
- **Observability**: Application Insights integration with structured logging
- **Performance**: Output caching, response compression, and connection pooling
- **Security**: Azure AD authentication, CORS, rate limiting

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
    "ManagedIdentityClientId": null,  // or user-assigned identity client ID
    "DefaultModelDeployment": "gpt-4o"
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-api-client-id",
    "Audience": "api://your-api-client-id"
  },
  "ApplicationInsights": {
    "ConnectionString": "your-app-insights-connection-string"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://your-spa-domain.com"
    ]
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
    AzureAI__DefaultModelDeployment="gpt-4o" \
    ApplicationInsights__ConnectionString="..."
```

### 4. Local Development

```bash
# Run locally
cd src/ATMET.AI.Api
dotnet run

# Navigate to Swagger UI
# https://localhost:5001
```

For local development, use `DefaultAzureCredential` which will attempt:

1. Azure CLI credentials (`az login`)
2. Visual Studio credentials
3. VS Code credentials
4. Managed Identity (when deployed)

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

```bash
https://your-app-service.azurewebsites.net/api/v1
```

### Authentication

All endpoints require Bearer token authentication:

```http
Authorization: Bearer {azure-ad-token}
```

### Endpoints Overview

#### Agents

- `POST /agents` - Create agent
- `GET /agents` - List agents
- `GET /agents/{id}` - Get agent
- `POST /agents/{id}/threads` - Create conversation
- `POST /threads/{id}/messages` - Add message
- `POST /threads/{id}/runs` - Execute agent

#### Deployments

- `GET /deployments` - List AI models
- `GET /deployments/{name}` - Get model details

#### Connections

- `GET /connections` - List connections
- `GET /connections/{name}` - Get connection

#### Datasets

- `POST /datasets/upload/file` - Upload file
- `GET /datasets` - List datasets
- `DELETE /datasets/{name}/versions/{version}` - Delete dataset

#### Indexes

- `POST /indexes` - Create index
- `GET /indexes` - List indexes
- `GET /indexes/{name}/versions/{version}` - Get index

#### Chat

- `POST /chat/completions` - Chat completion
- `POST /chat/completions/stream` - Streaming completion

Full API documentation available at `/swagger` when running.

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

- **Authentication**: Azure AD Bearer tokens
- **Authorization**: Role-based access control
- **CORS**: Configurable allowed origins
- **HTTPS**: Enforced in production
- **Managed Identity**: No credentials in code
- **Security Headers**: X-Content-Type-Options, X-Frame-Options, etc.

## ⚡ Performance

- **Output Caching**: Cached responses for deployments, connections
- **Response Compression**: Gzip compression
- **Connection Pooling**: Singleton Azure AI clients
- **Async/Await**: Non-blocking operations throughout
- **Minimal APIs**: Reduced overhead vs. controllers

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
- Email: <ai-team@atmet.com>

## 🗺️ Roadmap

- [ ] Batch processing support
- [ ] WebSocket support for real-time streaming
- [ ] Multi-tenancy support
- [ ] Custom tool integrations
- [ ] Enhanced caching strategies
- [ ] GraphQL endpoint support
- [ ] OpenTelemetry integration

## 📚 Additional Resources

- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
- [Azure AI Projects SDK](https://learn.microsoft.com/dotnet/api/overview/azure/ai.projects-readme)
- [Azure OpenAI Service](https://learn.microsoft.com/azure/ai-services/openai/)
- [.NET 10 Documentation](https://learn.microsoft.com/dotnet/)
