# ATMET AI Service - API Reference

Complete API documentation for all endpoints in the ATMET AI Service.

## Base URL

```
https://your-app-service.azurewebsites.net/api/v1
```

## Authentication

All API endpoints require Bearer token authentication using Azure AD:

```http
Authorization: Bearer {your-azure-ad-token}
```

### Getting a Token

```bash
# Using Azure CLI
az account get-access-token --resource api://your-api-client-id
```

## Common Response Codes

- `200 OK` - Request succeeded
- `201 Created` - Resource created successfully
- `204 No Content` - Request succeeded with no response body
- `400 Bad Request` - Invalid request parameters
- `401 Unauthorized` - Missing or invalid authentication
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `409 Conflict` - Resource conflict
- `429 Too Many Requests` - Rate limit exceeded
- `500 Internal Server Error` - Server error

## Agents API

### Create Agent

Create a new AI agent.

```http
POST /agents
```

**Request Body:**

```json
{
  "model": "gpt-4o",
  "name": "Customer Support Agent",
  "instructions": "You are a helpful customer support agent...",
  "tools": [
    {
      "type": "code_interpreter"
    },
    {
      "type": "file_search"
    }
  ],
  "metadata": {
    "department": "support",
    "version": "1.0"
  }
}
```

**Response:** `201 Created`

```json
{
  "id": "agent_abc123",
  "name": "Customer Support Agent",
  "model": "gpt-4o",
  "instructions": "You are a helpful...",
  "createdAt": "2026-02-10T10:30:00Z",
  "metadata": {
    "department": "support",
    "version": "1.0"
  }
}
```

### List Agents

List all agents.

```http
GET /agents?limit=20&order=desc
```

**Query Parameters:**

- `limit` (optional): Maximum number of results (default: 20)
- `order` (optional): Sort order - `asc` or `desc` (default: desc)

**Response:** `200 OK`

```json
[
  {
    "id": "agent_abc123",
    "name": "Customer Support Agent",
    "model": "gpt-4o",
    "instructions": "...",
    "createdAt": "2026-02-10T10:30:00Z",
    "metadata": {}
  }
]
```

### Get Agent

Get agent details.

```http
GET /agents/{agentId}
```

**Response:** `200 OK`

```json
{
  "id": "agent_abc123",
  "name": "Customer Support Agent",
  "model": "gpt-4o",
  "instructions": "...",
  "createdAt": "2026-02-10T10:30:00Z",
  "metadata": {}
}
```

### Update Agent

Update an existing agent.

```http
PUT /agents/{agentId}
```

**Request Body:**

```json
{
  "name": "Updated Agent Name",
  "instructions": "New instructions..."
}
```

**Response:** `200 OK` - Updated agent object

### Delete Agent

Delete an agent.

```http
DELETE /agents/{agentId}
```

**Response:** `204 No Content`

### Create Thread

Create a conversation thread.

```http
POST /agents/{agentId}/threads
```

**Request Body (optional):**

```json
{
  "metadata": {
    "userId": "user123",
    "sessionId": "session456"
  }
}
```

**Response:** `201 Created`

```json
{
  "id": "thread_xyz789",
  "createdAt": "2026-02-10T10:35:00Z",
  "metadata": {
    "userId": "user123"
  }
}
```

### Add Message

Add a message to a thread.

```http
POST /threads/{threadId}/messages
```

**Request Body:**

```json
{
  "role": "user",
  "content": "Hello, I need help with my account.",
  "fileIds": ["file_123", "file_456"]
}
```

**Response:** `201 Created`

```json
{
  "id": "msg_001",
  "threadId": "thread_xyz789",
  "role": "user",
  "content": "Hello, I need help...",
  "createdAt": "2026-02-10T10:36:00Z",
  "fileIds": ["file_123"]
}
```

### Get Messages

Get all messages in a thread.

```http
GET /threads/{threadId}/messages?limit=50&order=asc
```

**Response:** `200 OK` - Array of message objects

### Create Run

Execute an agent on a thread.

```http
POST /threads/{threadId}/runs
```

**Request Body:**

```json
{
  "agentId": "agent_abc123",
  "instructions": "Please be concise in your responses.",
  "metadata": {
    "environment": "production"
  }
}
```

**Response:** `201 Created`

```json
{
  "id": "run_123",
  "threadId": "thread_xyz789",
  "agentId": "agent_abc123",
  "status": "queued",
  "createdAt": "2026-02-10T10:37:00Z",
  "completedAt": null,
  "lastError": null
}
```

**Run Status Values:**

- `queued` - Run is waiting to be processed
- `in_progress` - Run is currently executing
- `requires_action` - Run requires user action (function calling)
- `completed` - Run finished successfully
- `failed` - Run failed
- `cancelled` - Run was cancelled

### Get Run

Get run status.

```http
GET /threads/{threadId}/runs/{runId}
```

**Response:** `200 OK` - Run object with current status

### Cancel Run

Cancel a running execution.

```http
POST /threads/{threadId}/runs/{runId}/cancel
```

**Response:** `200 OK` - Updated run object

## Deployments API

### List Deployments

List all AI model deployments.

```http
GET /deployments?modelPublisher=OpenAI&modelType=chat
```

**Query Parameters:**

- `modelPublisher` (optional): Filter by publisher
- `modelType` (optional): Filter by model type

**Response:** `200 OK`

```json
[
  {
    "name": "gpt-4o",
    "model": "gpt-4o-2024-05-13",
    "publisher": "OpenAI",
    "type": "chat",
    "status": "active"
  },
  {
    "name": "gpt-35-turbo",
    "model": "gpt-35-turbo-0125",
    "publisher": "OpenAI",
    "type": "chat",
    "status": "active"
  }
]
```

### Get Deployment

Get deployment details.

```http
GET /deployments/{deploymentName}
```

**Response:** `200 OK`

```json
{
  "name": "gpt-4o",
  "model": "gpt-4o-2024-05-13",
  "publisher": "OpenAI",
  "type": "chat",
  "status": "active"
}
```

## Connections API

### List Connections

List Azure resource connections.

```http
GET /connections?connectionType=AzureOpenAI&includeCredentials=false
```

**Query Parameters:**

- `connectionType` (optional): Filter by type (AzureOpenAI, AzureAISearch, etc.)
- `includeCredentials` (optional): Include connection credentials (default: false)

**Response:** `200 OK`

```json
[
  {
    "name": "my-openai-connection",
    "type": "AzureOpenAI",
    "category": "AIServices",
    "target": "https://my-openai.openai.azure.com",
    "properties": {
      "apiVersion": "2024-02-01"
    }
  }
]
```

### Get Connection

Get connection details.

```http
GET /connections/{connectionName}?includeCredentials=true
```

**Response:** `200 OK`

```json
{
  "name": "my-openai-connection",
  "type": "AzureOpenAI",
  "category": "AIServices",
  "target": "https://my-openai.openai.azure.com",
  "properties": {
    "apiVersion": "2024-02-01",
    "apiKey": "***" // Only if includeCredentials=true
  }
}
```

### Get Default Connection

Get the default project connection.

```http
GET /connections/default
```

## Datasets API

### Upload File

Upload a single file as a dataset.

```http
POST /datasets/upload/file
Content-Type: multipart/form-data
```

**Form Data:**

- `name`: Dataset name
- `version`: Dataset version (e.g., "1.0")
- `connectionName`: Storage connection name
- `file`: File to upload

**Response:** `201 Created`

```json
{
  "id": "dataset_123",
  "name": "training-data",
  "version": "1.0",
  "type": "file",
  "createdAt": "2026-02-10T10:40:00Z"
}
```

### Upload Folder

Upload multiple files as a dataset.

```http
POST /datasets/upload/folder
Content-Type: multipart/form-data
```

**Form Data:**

- `name`: Dataset name
- `version`: Dataset version
- `connectionName`: Storage connection name
- `files`: Multiple files
- `filePattern` (optional): Regex pattern for file filtering

**Response:** `201 Created`

### List Datasets

List all datasets (latest versions).

```http
GET /datasets
```

**Response:** `200 OK` - Array of dataset objects

### Get Dataset

Get specific dataset version.

```http
GET /datasets/{name}/versions/{version}
```

**Response:** `200 OK`

### Delete Dataset

Delete a dataset version.

```http
DELETE /datasets/{name}/versions/{version}
```

**Response:** `204 No Content`

## Indexes API

### Create/Update Index

Create or update a search index.

```http
POST /indexes
```

**Request Body:**

```json
{
  "name": "product-search",
  "version": "1.0",
  "connectionName": "my-search-connection",
  "indexName": "products-index",
  "description": "Product search index"
}
```

**Response:** `201 Created`

```json
{
  "id": "index_123",
  "name": "product-search",
  "version": "1.0",
  "connectionName": "my-search-connection",
  "indexName": "products-index",
  "description": "Product search index",
  "createdAt": "2026-02-10T10:45:00Z"
}
```

### List Indexes

List all indexes.

```http
GET /indexes
```

**Response:** `200 OK` - Array of index objects

### Get Index

Get specific index version.

```http
GET /indexes/{name}/versions/{version}
```

**Response:** `200 OK`

### Delete Index

Delete an index version.

```http
DELETE /indexes/{name}/versions/{version}
```

**Response:** `204 No Content`

## Chat API

### Create Completion

Create a chat completion.

```http
POST /chat/completions
```

**Request Body:**

```json
{
  "model": "gpt-4o",
  "messages": [
    {
      "role": "system",
      "content": "You are a helpful assistant."
    },
    {
      "role": "user",
      "content": "What is the capital of France?"
    }
  ],
  "temperature": 0.7,
  "maxTokens": 500
}
```

**Response:** `200 OK`

```json
{
  "id": "chatcmpl_123",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "The capital of France is Paris."
      },
      "finishReason": "stop"
    }
  ],
  "usage": {
    "promptTokens": 25,
    "completionTokens": 8,
    "totalTokens": 33
  },
  "created": "2026-02-10T10:50:00Z"
}
```

### Streaming Completion

Create a streaming chat completion.

```http
POST /chat/completions/stream
```

**Request Body:** Same as Create Completion

**Response:** `200 OK`

```
Content-Type: text/event-stream

data: {"id":"chatcmpl_123","choices":[{"index":0,"content":"The","finishReason":null}],"created":"2026-02-10T10:50:00Z"}

data: {"id":"chatcmpl_123","choices":[{"index":0,"content":" capital","finishReason":null}],"created":"2026-02-10T10:50:00Z"}

data: {"id":"chatcmpl_123","choices":[{"index":0,"content":" of","finishReason":null}],"created":"2026-02-10T10:50:00Z"}

data: [DONE]
```

## Health Check API

### Health Status

Check overall API health.

```http
GET /health
```

**Response:** `200 OK`

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.1234567",
  "entries": {
    "self": {
      "status": "Healthy"
    },
    "azure-ai-foundry": {
      "status": "Healthy",
      "duration": "00:00:00.1000000"
    }
  }
}
```

### Readiness Probe

Check if API is ready to serve requests.

```http
GET /health/ready
```

### Liveness Probe

Check if API is alive.

```http
GET /health/live
```

## Rate Limiting

API enforces rate limits:

- **Per User**: 100 requests per minute
- **Streaming**: 10 concurrent streams per user

Rate limit headers:

```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 2026-02-10T10:51:00Z
```

## Error Responses

All errors follow RFC 7807 Problem Details:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Agent with ID 'agent_invalid' not found",
  "instance": "/api/v1/agents/agent_invalid",
  "traceId": "00-1234567890abcdef-1234567890abcdef-00",
  "timestamp": "2026-02-10T10:52:00Z"
}
```
