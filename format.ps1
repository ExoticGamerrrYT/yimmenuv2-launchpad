Write-Host "Formatting code..." -ForegroundColor Blue

dotnet csharpier format .

if ($LASTEXITCODE -eq 0) {
    Write-Host "Success." -ForegroundColor Green
} else {
    Write-Host "Error." -ForegroundColor Red
}
