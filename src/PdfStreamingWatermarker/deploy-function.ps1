# Parameters
param(
    [Parameter(Mandatory=$true)]
    [string]$resourceGroupName,

    [Parameter(Mandatory=$true)]
    [string]$location,

    [Parameter(Mandatory=$true)]
    [string]$functionAppName,

    [Parameter(Mandatory=$true)]
    [string]$existingStorageAccountName,

    [Parameter(Mandatory=$true)]
    [string]$existingStorageResourceGroup,

    [Parameter(Mandatory=$true)]
    [string]$containerName = "pdfs"
)

# Ensure Azure CLI is logged in
Write-Host "Checking Azure CLI login status..." -ForegroundColor Yellow
$loginStatus = az account show --query "user.name" -o tsv
if (!$loginStatus) {
    Write-Host "Please login to Azure CLI first" -ForegroundColor Red
    az login
}

# Create Resource Group if it doesn't exist
Write-Host "Creating Resource Group if it doesn't exist..." -ForegroundColor Yellow
az group create --name $resourceGroupName --location $location

# Get existing Storage Account Connection String
Write-Host "Getting Storage Account Connection String..." -ForegroundColor Yellow
$storageConnectionString = $(az storage account show-connection-string `
    --name $existingStorageAccountName `
    --resource-group $existingStorageResourceGroup `
    --query connectionString `
    --output tsv)

# Create Linux Consumption Plan Function App
Write-Host "Creating Linux Function App..." -ForegroundColor Yellow
az functionapp create `
    --name $functionAppName `
    --resource-group $resourceGroupName `
    --storage-account $existingStorageAccountName `
    --consumption-plan-location $location `
    --runtime dotnet-isolated `
    --runtime-version 8 `
    --functions-version 4 `
    --os-type Linux

# Configure Function App Settings
Write-Host "Configuring Function App Settings..." -ForegroundColor Yellow
az functionapp config appsettings set `
    --name $functionAppName `
    --resource-group $resourceGroupName `
    --settings `
    "AzureStorage:ConnectionString=$storageConnectionString" `
    "AzureStorage:ContainerName=$containerName" `
    "WEBSITE_RUN_FROM_PACKAGE=1" `
    "FUNCTIONS_WORKER_PROCESS_COUNT=8" `
    "WEBSITE_MAX_DYNAMIC_APPLICATION_SCALE_OUT=10" `
    "WEBSITE_WARMUP_PATH=/api/WatermarkPdf?warmup=true"

# Build and Publish Function App
Write-Host "Building and Publishing Function App..." -ForegroundColor Yellow
dotnet publish -c Release
$publishPath = ".\bin\Release\net8.0\publish"

# Create zip deployment package
$deploymentZip = "deployment.zip"
Compress-Archive -Path "$publishPath\*" -DestinationPath $deploymentZip -Force

# Deploy using zip deployment
Write-Host "Deploying Function App..." -ForegroundColor Yellow
az functionapp deployment source config-zip `
    --name $functionAppName `
    --resource-group $resourceGroupName `
    --src $deploymentZip

# Cleanup
Remove-Item $deploymentZip -Force

Write-Host "Deployment completed!" -ForegroundColor Green
Write-Host "Function App URL: https://$functionAppName.azurewebsites.net" -ForegroundColor Green 