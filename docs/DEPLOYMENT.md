# ATMET AI Service - Deployment Guide

This guide provides detailed instructions for deploying the ATMET AI Service to Azure App Service using Managed Identity.

## 📋 Prerequisites

- Azure CLI installed and configured
- .NET 10 SDK installed
- Azure subscription with appropriate permissions
- Azure AI Foundry project already created

## 🏗️ Infrastructure Setup

### 1. Create Resource Group

```bash
# Set variables
RESOURCE_GROUP="rg-atmet-ai-prod"
LOCATION="eastus"
APP_NAME="atmet-ai-service"
APP_INSIGHTS_NAME="ai-atmet-service"

# Create resource group
az group create \
    --name $RESOURCE_GROUP \
    --location $LOCATION
```

### 2. Create Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
    --app $APP_INSIGHTS_NAME \
    --location $LOCATION \
    --resource-group $RESOURCE_GROUP \
    --application-type web

# Get instrumentation key and connection string
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
    --app $APP_INSIGHTS_NAME \
    --resource-group $RESOURCE_GROUP \
    --query instrumentationKey -o tsv)

CONNECTION_STRING=$(az monitor app-insights component show \
    --app $APP_INSIGHTS_NAME \
    --resource-group $RESOURCE_GROUP \
    --query connectionString -o tsv)
```

### 3. Create App Service Plan

```bash
# Create App Service Plan (Linux, P1v3)
az appservice plan create \
    --name "${APP_NAME}-plan" \
    --resource-group $RESOURCE_GROUP \
    --location $LOCATION \
    --is-linux \
    --sku P1v3
```

### 4. Create Web App

```bash
# Create Web App with .NET 10 runtime
az webapp create \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --plan "${APP_NAME}-plan" \
    --runtime "DOTNET|10.0"

# Enable HTTPS only
az webapp update \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --https-only true
```

### 5. Configure Managed Identity

#### Option A: System-Assigned Managed Identity

```bash
# Enable system-assigned managed identity
az webapp identity assign \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP

# Get the principal ID
PRINCIPAL_ID=$(az webapp identity show \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --query principalId -o tsv)

echo "Managed Identity Principal ID: $PRINCIPAL_ID"
```

#### Option B: User-Assigned Managed Identity

```bash
# Create user-assigned identity
IDENTITY_NAME="id-atmet-ai-service"

az identity create \
    --name $IDENTITY_NAME \
    --resource-group $RESOURCE_GROUP

# Get the identity details
IDENTITY_ID=$(az identity show \
    --name $IDENTITY_NAME \
    --resource-group $RESOURCE_GROUP \
    --query id -o tsv)

CLIENT_ID=$(az identity show \
    --name $IDENTITY_NAME \
    --resource-group $RESOURCE_GROUP \
    --query clientId -o tsv)

PRINCIPAL_ID=$(az identity show \
    --name $IDENTITY_NAME \
    --resource-group $RESOURCE_GROUP \
    --query principalId -o tsv)

# Assign to Web App
az webapp identity assign \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --identities $IDENTITY_ID

echo "User-Assigned Identity Client ID: $CLIENT_ID"
echo "Principal ID: $PRINCIPAL_ID"
```

### 6. Grant Azure Permissions

```bash
# Get Azure AI Foundry resource details
AI_FOUNDRY_RG="rg-ai-foundry"  # Your AI Foundry resource group
AI_FOUNDRY_SCOPE="/subscriptions/{subscription-id}/resourceGroups/${AI_FOUNDRY_RG}"

# Assign Cognitive Services User role
az role assignment create \
    --role "Cognitive Services User" \
    --assignee-object-id $PRINCIPAL_ID \
    --assignee-principal-type ServicePrincipal \
    --scope $AI_FOUNDRY_SCOPE

# Assign Cognitive Services OpenAI User (if using OpenAI)
az role assignment create \
    --role "Cognitive Services OpenAI User" \
    --assignee-object-id $PRINCIPAL_ID \
    --assignee-principal-type ServicePrincipal \
    --scope $AI_FOUNDRY_SCOPE

# If using Azure AI Search
SEARCH_RESOURCE_NAME="your-search-service"
SEARCH_SCOPE="/subscriptions/{subscription-id}/resourceGroups/${AI_FOUNDRY_RG}/providers/Microsoft.Search/searchServices/${SEARCH_RESOURCE_NAME}"

az role assignment create \
    --role "Search Index Data Contributor" \
    --assignee-object-id $PRINCIPAL_ID \
    --assignee-principal-type ServicePrincipal \
    --scope $SEARCH_SCOPE

# If using Storage for datasets
STORAGE_ACCOUNT="yourstorageaccount"
STORAGE_SCOPE="/subscriptions/{subscription-id}/resourceGroups/${AI_FOUNDRY_RG}/providers/Microsoft.Storage/storageAccounts/${STORAGE_ACCOUNT}"

az role assignment create \
    --role "Storage Blob Data Contributor" \
    --assignee-object-id $PRINCIPAL_ID \
    --assignee-principal-type ServicePrincipal \
    --scope $STORAGE_SCOPE
```

### 7. Configure Application Settings

```bash
# Azure AI Configuration
az webapp config appsettings set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
    AzureAI__ProjectEndpoint="https://your-resource.services.ai.azure.com/api/projects/your-project-id" \
    AzureAI__DefaultModelDeployment="gpt-4o" \
    AzureAI__EnableTelemetry="true" \
    AzureAI__RequestTimeoutSeconds="120" \
    AzureAI__MaxRetryAttempts="3"

# If using user-assigned identity
az webapp config appsettings set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
    AzureAI__ManagedIdentityClientId="$CLIENT_ID"

# Application Insights
az webapp config appsettings set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
    ApplicationInsights__ConnectionString="$CONNECTION_STRING" \
    ApplicationInsights__EnableAdaptiveSampling="true"

# CORS Configuration (replace with your SPA domain)
az webapp config appsettings set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
    Cors__AllowedOrigins__0="https://your-spa.azurewebsites.net" \
    Cors__AllowedOrigins__1="http://localhost:3000"

# Azure AD Configuration (if using Azure AD auth)
az webapp config appsettings set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings \
    AzureAd__Instance="https://login.microsoftonline.com/" \
    AzureAd__TenantId="your-tenant-id" \
    AzureAd__ClientId="your-api-client-id" \
    AzureAd__Audience="api://your-api-client-id"
```

### 8. Configure Logging

```bash
# Enable Application Logging
az webapp log config \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --application-logging azureblobstorage \
    --level information \
    --web-server-logging filesystem
```

### 9. Configure Health Check

```bash
# Enable health check
az webapp config set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --health-check-path "/health/ready"
```

## 🚀 Deployment

### Method 1: ZIP Deployment (Recommended for CI/CD)

```bash
# Build and publish
dotnet publish ./src/ATMET.AI.Api/ATMET.AI.Api.csproj \
    -c Release \
    -o ./publish

# Create ZIP
cd publish
zip -r ../publish.zip .
cd ..

# Deploy
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_NAME \
    --src-path publish.zip \
    --type zip
```

### Method 2: GitHub Actions

Create `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure App Service

on:
  push:
    branches: [ main ]

env:
  AZURE_WEBAPP_NAME: atmet-ai-service
  DOTNET_VERSION: '10.0.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
    
    - name: Publish
      run: dotnet publish ./src/ATMET.AI.Api/ATMET.AI.Api.csproj -c Release -o ./publish
    
    - name: Deploy to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ env.AZURE_WEBAPP_NAME }}
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ./publish
```

### Method 3: Azure DevOps Pipeline

Create `azure-pipelines.yml`:

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'
  azureSubscription: 'YourAzureConnection'
  webAppName: 'atmet-ai-service'

steps:
- task: UseDotNet@2
  inputs:
    version: '10.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Restore'
  inputs:
    command: 'restore'

- task: DotNetCoreCLI@2
  displayName: 'Build'
  inputs:
    command: 'build'
    arguments: '--configuration $(buildConfiguration)'

- task: DotNetCoreCLI@2
  displayName: 'Test'
  inputs:
    command: 'test'
    arguments: '--configuration $(buildConfiguration) --no-build'

- task: DotNetCoreCLI@2
  displayName: 'Publish'
  inputs:
    command: 'publish'
    publishWebProjects: false
    projects: 'src/ATMET.AI.Api/ATMET.AI.Api.csproj'
    arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)'

- task: AzureWebApp@1
  displayName: 'Deploy to Azure App Service'
  inputs:
    azureSubscription: $(azureSubscription)
    appName: $(webAppName)
    package: '$(Build.ArtifactStagingDirectory)/**/*.zip'
```

## ✅ Post-Deployment Verification

### 1. Check Health

```bash
# Check if app is running
curl https://${APP_NAME}.azurewebsites.net/health

# Check readiness
curl https://${APP_NAME}.azurewebsites.net/health/ready

# Check liveness
curl https://${APP_NAME}.azurewebsites.net/health/live
```

### 2. Test API

```bash
# Get Azure AD token
TOKEN=$(az account get-access-token \
    --resource api://your-api-client-id \
    --query accessToken -o tsv)

# Test deployments endpoint
curl -H "Authorization: Bearer $TOKEN" \
    https://${APP_NAME}.azurewebsites.net/api/v1/deployments

# Test connections endpoint
curl -H "Authorization: Bearer $TOKEN" \
    https://${APP_NAME}.azurewebsites.net/api/v1/connections
```

### 3. View Logs

```bash
# Stream logs
az webapp log tail \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP

# Download logs
az webapp log download \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --log-file logs.zip
```

## 🔍 Monitoring

### Application Insights Queries

```kusto
// Request failures
requests
| where success == false
| summarize count() by resultCode, name
| order by count_ desc

// Slow requests
requests
| where duration > 1000
| project timestamp, name, duration, resultCode
| order by duration desc

// Dependency failures
dependencies
| where success == false
| summarize count() by name, resultCode

// Exceptions
exceptions
| project timestamp, type, outerMessage, operation_Name
| order by timestamp desc
```

## 🔒 Security Hardening

```bash
# Disable FTP
az webapp config set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --ftps-state Disabled

# Minimum TLS version
az webapp config set \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --min-tls-version 1.2

# Enable authentication (if needed)
az webapp auth update \
    --name $APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --enabled true \
    --action LoginWithAzureActiveDirectory
```

## 📊 Scaling

```bash
# Manual scale
az appservice plan update \
    --name "${APP_NAME}-plan" \
    --resource-group $RESOURCE_GROUP \
    --number-of-workers 3

# Auto-scale
az monitor autoscale create \
    --resource-group $RESOURCE_GROUP \
    --resource "${APP_NAME}-plan" \
    --resource-type Microsoft.Web/serverfarms \
    --name "${APP_NAME}-autoscale" \
    --min-count 1 \
    --max-count 10 \
    --count 2

# Scale rule based on CPU
az monitor autoscale rule create \
    --resource-group $RESOURCE_GROUP \
    --autoscale-name "${APP_NAME}-autoscale" \
    --condition "Percentage CPU > 70 avg 5m" \
    --scale out 1

az monitor autoscale rule create \
    --resource-group $RESOURCE_GROUP \
    --autoscale-name "${APP_NAME}-autoscale" \
    --condition "Percentage CPU < 30 avg 5m" \
    --scale in 1
```

## 🧹 Cleanup

```bash
# Delete the entire resource group
az group delete \
    --name $RESOURCE_GROUP \
    --yes \
    --no-wait
```

## 📝 Troubleshooting

### Issue: 403 Forbidden errors

**Solution**: Verify managed identity has correct role assignments
```bash
az role assignment list \
    --assignee $PRINCIPAL_ID \
    --all
```

### Issue: Health check fails

**Solution**: Check logs and ensure Azure AI endpoint is accessible
```bash
az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP
```

### Issue: Slow performance

**Solution**: Enable output caching and scale up
```bash
az appservice plan update \
    --name "${APP_NAME}-plan" \
    --resource-group $RESOURCE_GROUP \
    --sku P2v3
```

## 🔗 Additional Resources

- [Azure App Service Documentation](https://learn.microsoft.com/azure/app-service/)
- [Managed Identities Documentation](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)
- [Azure AI Foundry Documentation](https://learn.microsoft.com/azure/ai-studio/)
