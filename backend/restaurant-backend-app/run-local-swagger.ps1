param(
    [string]$FunctionName = "api-handler:dev",
    [string]$Region = "eu-north-1"
)

$ErrorActionPreference = "Stop"

$functionConfigJson = & aws lambda get-function-configuration `
    --function-name $FunctionName `
    --region $Region `
    --query "Environment.Variables" `
    --output json

if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($functionConfigJson)) {
    throw "Could not read the Lambda environment. Verify AWS CLI credentials and access to '$FunctionName' in '$Region'."
}

$functionEnvironment = $functionConfigJson | ConvertFrom-Json
foreach ($variable in $functionEnvironment.PSObject.Properties) {
    if ($null -ne $variable.Value) {
        [Environment]::SetEnvironmentVariable($variable.Name, [string]$variable.Value, "Process")
    }
}

$env:AWS_REGION = $Region
$env:AWS_DEFAULT_REGION = $Region
$env:ASPNETCORE_URLS = "http://127.0.0.1:5187"
$env:OPEN_SWAGGER = "true"

Write-Host "Starting the local API against AWS resources from '$FunctionName'."
Write-Host "Swagger UI: http://127.0.0.1:5187/swagger"

dotnet run --project (Join-Path $PSScriptRoot "LocalApi\LocalApi.csproj")
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
