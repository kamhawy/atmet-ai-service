# Contributing to ATMET AI Service

Thank you for your interest in contributing to the ATMET AI Service! This document provides guidelines and instructions for contributing.

## Code of Conduct

This project adheres to the Contributor Covenant Code of Conduct. By participating, you are expected to uphold this code.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check existing issues. When creating a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide specific examples**
- **Describe the behavior you observed and what you expected**
- **Include screenshots if applicable**
- **Note your environment** (OS, .NET version, Azure region, etc.)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description of the suggested enhancement**
- **Explain why this enhancement would be useful**
- **List any alternative solutions you've considered**

### Pull Requests

1. **Fork the repository** and create your branch from `develop`
2. **Follow the code style** used throughout the project
3. **Write or update tests** as necessary
4. **Update documentation** to reflect your changes
5. **Ensure CI/CD passes** before requesting review
6. **Link to related issues** in your PR description

## Development Setup

### Prerequisites

- .NET 10 SDK
- Visual Studio 2024 or VS Code
- Azure subscription (for testing)
- Git

### Local Development

1. Clone the repository:
```bash
git clone https://github.com/kamhawy/atmet-ai-service.git
cd atmet-ai-service
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Configure settings:
```bash
cd src/ATMET.AI.Api
cp appsettings.json appsettings.Development.json
# Edit appsettings.Development.json with your Azure credentials
```

4. Run the application:
```bash
dotnet run
```

5. Run tests:
```bash
dotnet test
```

## Coding Standards

### C# Style Guide

- Use **C# 12** features appropriately
- Follow **Microsoft's C# Coding Conventions**
- Use **nullable reference types**
- Prefer **records** for DTOs
- Use **async/await** for I/O operations
- Follow **SOLID principles**

### Naming Conventions

- **PascalCase** for classes, methods, properties
- **camelCase** for local variables, parameters
- **_camelCase** for private fields
- **UPPER_CASE** for constants
- Prefix interfaces with **I** (e.g., `IAgentService`)

### Code Organization

```csharp
// 1. Using statements (grouped and sorted)
using System;
using System.Collections.Generic;
using Azure.AI.Projects;
using ATMET.AI.Core.Services;

// 2. Namespace
namespace ATMET.AI.Infrastructure.Services;

// 3. Class documentation
/// <summary>
/// Brief description of the class
/// </summary>
public class MyService : IMyService
{
    // 4. Private fields
    private readonly IDependency _dependency;
    private readonly ILogger<MyService> _logger;
    
    // 5. Constructor
    public MyService(IDependency dependency, ILogger<MyService> logger)
    {
        _dependency = dependency;
        _logger = logger;
    }
    
    // 6. Public methods
    public async Task<Result> DoSomethingAsync(Request request)
    {
        // Implementation
    }
    
    // 7. Private methods
    private void HelperMethod()
    {
        // Implementation
    }
}
```

### Documentation

- Add XML documentation comments for:
  - All public classes and interfaces
  - All public methods and properties
  - Complex private methods

```csharp
/// <summary>
/// Creates a new agent with the specified configuration
/// </summary>
/// <param name="request">The agent creation request</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>The created agent response</returns>
/// <exception cref="ValidationException">Thrown when request is invalid</exception>
public async Task<AgentResponse> CreateAgentAsync(
    CreateAgentRequest request,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### Error Handling

- Use **try-catch** for expected errors
- Log errors with **structured logging**
- Throw **custom exceptions** for domain errors
- Never swallow exceptions silently

```csharp
try
{
    _logger.LogInformation("Performing operation for {Id}", id);
    var result = await _service.DoSomethingAsync(id);
    return result;
}
catch (NotFoundException ex)
{
    _logger.LogWarning(ex, "Resource not found: {Id}", id);
    throw;
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error for {Id}", id);
    throw;
}
```

### Testing

- Write **unit tests** for business logic
- Write **integration tests** for API endpoints
- Aim for **>80% code coverage**
- Use **meaningful test names**
- Follow **AAA pattern** (Arrange, Act, Assert)

```csharp
[Fact]
public async Task CreateAgent_WithValidRequest_ReturnsAgent()
{
    // Arrange
    var request = new CreateAgentRequest(
        Model: "gpt-4o",
        Name: "Test Agent"
    );
    
    // Act
    var result = await _service.CreateAgentAsync(request);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal("Test Agent", result.Name);
}
```

## Git Commit Messages

- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit the first line to 72 characters
- Reference issues and pull requests

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

**Example:**
```
feat(agents): add support for file attachments

Implements file upload functionality for agents including:
- File validation
- Azure storage integration
- Metadata tracking

Closes #123
```

## Branch Naming

- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation updates
- `refactor/description` - Code refactoring
- `test/description` - Test additions/updates

## Pull Request Process

1. Update README.md with details of changes if applicable
2. Update API documentation for new endpoints
3. Ensure all tests pass
4. Update CHANGELOG.md following Keep a Changelog format
5. Request review from at least one maintainer
6. Squash commits before merging

## Release Process

1. Update version in .csproj files
2. Update CHANGELOG.md
3. Create a release branch
4. Tag the release
5. Deploy to staging
6. Run smoke tests
7. Deploy to production
8. Create GitHub release

## Questions?

Feel free to open an issue for any questions or join our discussions!

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
