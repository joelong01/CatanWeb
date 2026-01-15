<#
.SYNOPSIS
    Sets up Azure OIDC authentication for GitHub Actions.
.DESCRIPTION
    Creates an Azure AD App Registration with federated credentials for GitHub Actions.
    This allows the deploy-azure.yml workflow to authenticate without storing secrets.
.PARAMETER GitHubOrg
    GitHub organization or username (e.g., "joelong")
.PARAMETER GitHubRepo
    GitHub repository name (e.g., "CatanWeb")
.EXAMPLE
    ./setup-github-actions-azure.ps1 -GitHubOrg "joelong" -GitHubRepo "CatanWeb"
#>

param(
    [Parameter(Mandatory)]
    [string]$GitHubOrg,

    [Parameter(Mandatory)]
    [string]$GitHubRepo,

    [string]$AppName = "github-actions-catan-deploy"
)

$ErrorActionPreference = "Stop"

Write-Host "Setting up Azure OIDC for GitHub Actions..." -ForegroundColor Cyan
Write-Host ""

# Check Azure login
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged into Azure. Please run: az login" -ForegroundColor Red
    exit 1
}

Write-Host "Using Azure subscription: $($account.name)" -ForegroundColor Gray
Write-Host "Subscription ID: $($account.id)" -ForegroundColor Gray
Write-Host ""

# Get or create App Registration
Write-Host "Checking for existing App Registration: $AppName" -ForegroundColor Gray
$app = az ad app list --display-name $AppName 2>$null | ConvertFrom-Json | Select-Object -First 1

if ($app) {
    Write-Host "Found existing App Registration" -ForegroundColor Green
    $appId = $app.appId
}
else {
    Write-Host "Creating App Registration: $AppName" -ForegroundColor Yellow
    $app = az ad app create --display-name $AppName | ConvertFrom-Json
    $appId = $app.appId
    Write-Host "Created App Registration with ID: $appId" -ForegroundColor Green
}

# Get or create Service Principal
Write-Host "Checking for Service Principal..." -ForegroundColor Gray
$sp = az ad sp list --filter "appId eq '$appId'" 2>$null | ConvertFrom-Json | Select-Object -First 1

if (-not $sp) {
    Write-Host "Creating Service Principal..." -ForegroundColor Yellow
    $sp = az ad sp create --id $appId | ConvertFrom-Json
    Write-Host "Created Service Principal" -ForegroundColor Green
}
else {
    Write-Host "Service Principal exists" -ForegroundColor Green
}

# Assign Contributor role to resource group
$config = Get-Content ".azure/catan-azure.json" | ConvertFrom-Json
$rgName = $config.resourceGroup

Write-Host "Assigning Contributor role on resource group: $rgName" -ForegroundColor Gray
$roleAssignment = az role assignment create `
    --assignee $appId `
    --role "Contributor" `
    --scope "/subscriptions/$($account.id)/resourceGroups/$rgName" 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Role assignment created" -ForegroundColor Green
}
else {
    Write-Host "Role assignment may already exist (this is OK)" -ForegroundColor Yellow
}

# Create federated credential for main branch
$credentialName = "github-actions-main"
Write-Host "Creating federated credential for main branch..." -ForegroundColor Gray

$federatedCredential = @{
    name      = $credentialName
    issuer    = "https://token.actions.githubusercontent.com"
    subject   = "repo:${GitHubOrg}/${GitHubRepo}:ref:refs/heads/main"
    audiences = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Compress

# Check if credential exists
$existingCred = az ad app federated-credential list --id $appId 2>$null | ConvertFrom-Json | Where-Object { $_.name -eq $credentialName }

if ($existingCred) {
    Write-Host "Federated credential already exists" -ForegroundColor Green
}
else {
    az ad app federated-credential create --id $appId --parameters $federatedCredential | Out-Null
    Write-Host "Created federated credential for main branch" -ForegroundColor Green
}

# Output the values needed for GitHub secrets
$tenantId = $account.tenantId

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Add these secrets to your GitHub repository:" -ForegroundColor Yellow
Write-Host "  Settings > Secrets and variables > Actions > New repository secret" -ForegroundColor Gray
Write-Host ""
Write-Host "  AZURE_CLIENT_ID:       $appId" -ForegroundColor White
Write-Host "  AZURE_TENANT_ID:       $tenantId" -ForegroundColor White
Write-Host "  AZURE_SUBSCRIPTION_ID: $($account.id)" -ForegroundColor White
Write-Host ""
Write-Host "Or run this gh command to set them automatically:" -ForegroundColor Yellow
Write-Host ""
Write-Host "gh secret set AZURE_CLIENT_ID --body `"$appId`"" -ForegroundColor Cyan
Write-Host "gh secret set AZURE_TENANT_ID --body `"$tenantId`"" -ForegroundColor Cyan
Write-Host "gh secret set AZURE_SUBSCRIPTION_ID --body `"$($account.id)`"" -ForegroundColor Cyan
Write-Host ""
