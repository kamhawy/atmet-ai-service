# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial release of ATMET AI Service
- Complete REST API for Azure AI Foundry integration
- Agents management endpoints with full lifecycle support
- Deployments enumeration and management
- Azure resource connections management
- Datasets upload and versioning
- Search indexes creation and management
- Chat completions with streaming support
- Managed Identity authentication
- Health check endpoints
- Application Insights integration
- Structured logging with Serilog
- Output caching for performance
- Global exception handling
- Request/response logging middleware
- Comprehensive API documentation (Swagger/OpenAPI)
- GitHub Actions CI/CD pipeline
- Enterprise-grade code structure and best practices

### Security
- Azure AD Bearer token authentication
- Managed Identity for Azure service access
- CORS configuration
- HTTPS enforcement
- Security headers (X-Frame-Options, X-Content-Type-Options, etc.)

### Documentation
- Complete API reference
- Architecture documentation
- Deployment guide for Azure App Service
- Contributing guidelines
- Code of conduct
- Issue and PR templates

## [1.0.0] - 2026-02-10

### Added
- Initial project setup
- .NET 10 Web API with Minimal APIs
- Azure AI Projects SDK v1.1.0 integration
- Azure AI Agents.Persistent SDK v1.2.0-beta.2 integration
- Clean architecture implementation (API, Core, Infrastructure layers)
- Service-based architecture with dependency injection
- Production-ready middleware stack
- Comprehensive error handling and logging

[Unreleased]: https://github.com/kamhawy/atmet-ai-service/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/kamhawy/atmet-ai-service/releases/tag/v1.0.0
