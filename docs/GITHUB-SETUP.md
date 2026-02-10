# GitHub Setup Guide

This guide will help you push the ATMET AI Service project to your GitHub repository.

## Prerequisites

- Git installed on your system
- GitHub account with repository access
- Command line or terminal access

## Step 1: Initialize Git Repository (if not already done)

```bash
cd path/to/ATMET.AI.Service
git init
```

## Step 2: Add Remote Repository

```bash
git remote add origin https://github.com/kamhawy/atmet-ai-service.git
```

## Step 3: Configure Git User (if not already done)

```bash
git config user.name "Your Name"
git config user.email "your.email@example.com"
```

## Step 4: Stage All Files

```bash
git add .
```

## Step 5: Create Initial Commit

```bash
git commit -m "feat: initial commit with complete ATMET AI Service implementation

- Complete REST API for Azure AI Foundry integration
- Agents, Deployments, Connections, Datasets, Indexes, and Chat endpoints
- Managed Identity authentication
- Health checks and monitoring
- GitHub Actions CI/CD pipeline
- Comprehensive documentation"
```

## Step 6: Create Main Branch and Push

```bash
# Rename current branch to main (if needed)
git branch -M main

# Push to GitHub
git push -u origin main
```

## Alternative: If Repository Already Has Content

If your GitHub repository already has content, you may need to pull first:

```bash
# Pull existing content
git pull origin main --allow-unrelated-histories

# Resolve any conflicts if they exist

# Push your changes
git push -u origin main
```

## Step 7: Create Develop Branch (Recommended)

```bash
# Create and switch to develop branch
git checkout -b develop

# Push develop branch
git push -u origin develop
```

## Step 8: Configure GitHub Secrets

For CI/CD to work, configure these secrets in your GitHub repository:

1. Go to: `https://github.com/kamhawy/atmet-ai-service/settings/secrets/actions`

2. Add the following secrets:

### For Development Environment
- `AZURE_CREDENTIALS_DEV` - Azure service principal credentials for dev environment
  ```json
  {
    "clientId": "your-client-id",
    "clientSecret": "your-client-secret",
    "subscriptionId": "your-subscription-id",
    "tenantId": "your-tenant-id"
  }
  ```

### For Production Environment
- `AZURE_CREDENTIALS_PROD` - Azure service principal credentials for prod environment
  ```json
  {
    "clientId": "your-client-id",
    "clientSecret": "your-client-secret",
    "subscriptionId": "your-subscription-id",
    "tenantId": "your-tenant-id"
  }
  ```

### Optional Secrets
- `CODECOV_TOKEN` - For code coverage reporting (if using Codecov)

## Step 9: Create Azure Service Principal

To create Azure service principals for GitHub Actions:

```bash
# For Development
az ad sp create-for-rbac \
  --name "atmet-ai-service-dev" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/{dev-resource-group} \
  --sdk-auth

# For Production
az ad sp create-for-rbac \
  --name "atmet-ai-service-prod" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/{prod-resource-group} \
  --sdk-auth
```

Copy the JSON output to the respective GitHub secrets.

## Step 10: Set Up Branch Protection (Recommended)

1. Go to: `https://github.com/kamhawy/atmet-ai-service/settings/branches`

2. Add rule for `main` branch:
   - ✅ Require a pull request before merging
   - ✅ Require approvals (at least 1)
   - ✅ Require status checks to pass before merging
   - ✅ Require branches to be up to date before merging
   - ✅ Include administrators

3. Add rule for `develop` branch:
   - ✅ Require pull request before merging
   - ✅ Require status checks to pass before merging

## Step 11: Verify GitHub Actions

1. Go to: `https://github.com/kamhawy/atmet-ai-service/actions`

2. You should see the CI/CD pipeline running

3. Verify all jobs complete successfully

## Common Git Commands

### Check Status
```bash
git status
```

### View Remote
```bash
git remote -v
```

### Pull Latest Changes
```bash
git pull origin main
```

### Create New Feature Branch
```bash
git checkout -b feature/my-new-feature
```

### Stage Specific Files
```bash
git add src/ATMET.AI.Api/Program.cs
```

### View Commit History
```bash
git log --oneline --graph
```

### Undo Last Commit (keep changes)
```bash
git reset --soft HEAD~1
```

## Troubleshooting

### Authentication Issues

If you have authentication issues:

1. **Use Personal Access Token (PAT)**:
   ```bash
   # Generate PAT at: https://github.com/settings/tokens
   # Use PAT as password when prompted
   ```

2. **Use SSH instead of HTTPS**:
   ```bash
   # Generate SSH key
   ssh-keygen -t ed25519 -C "your.email@example.com"
   
   # Add to GitHub: https://github.com/settings/keys
   
   # Change remote URL
   git remote set-url origin git@github.com:kamhawy/atmet-ai-service.git
   ```

### Large File Issues

If you get errors about large files:

```bash
# Check file sizes
find . -type f -size +50M

# Remove file from staging
git rm --cached path/to/large/file

# Add to .gitignore
echo "path/to/large/file" >> .gitignore
```

### Merge Conflicts

If you encounter merge conflicts:

```bash
# 1. Pull latest changes
git pull origin main

# 2. Resolve conflicts in your editor

# 3. Stage resolved files
git add .

# 4. Complete merge
git commit -m "Merge conflicts resolved"

# 5. Push changes
git push origin main
```

## Next Steps

After pushing to GitHub:

1. ✅ Verify repository structure on GitHub
2. ✅ Check GitHub Actions workflows are running
3. ✅ Configure repository settings and branch protection
4. ✅ Add collaborators if needed
5. ✅ Update repository description and topics
6. ✅ Add repository to organization (if applicable)
7. ✅ Configure issue labels and milestones
8. ✅ Set up project boards for tracking

## Repository Settings Checklist

- [ ] Repository description added
- [ ] Topics/tags added (csharp, dotnet, azure, ai, rest-api, etc.)
- [ ] License selected (MIT)
- [ ] README.md displays correctly
- [ ] Wiki enabled (optional)
- [ ] Issues enabled
- [ ] Discussions enabled (optional)
- [ ] Branch protection rules configured
- [ ] GitHub Actions secrets configured
- [ ] Dependabot alerts enabled
- [ ] Code scanning configured (optional)

## Resources

- [GitHub Documentation](https://docs.github.com)
- [Git Documentation](https://git-scm.com/doc)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Azure CLI Documentation](https://docs.microsoft.com/en-us/cli/azure/)

---

**Ready to push? Run these commands:**

```bash
cd path/to/ATMET.AI.Service
git init
git add .
git commit -m "feat: initial commit with complete implementation"
git branch -M main
git remote add origin https://github.com/kamhawy/atmet-ai-service.git
git push -u origin main
```

🎉 **You're all set!**
