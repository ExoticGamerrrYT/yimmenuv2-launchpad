Write-Host "Publicando aplicación..." -ForegroundColor Green
# Trim está deshabilitado para WPF y WinForms
# Not standalone
# dotnet publish -c Release -r win-x64 --self-contained true # net y extras al lado, rápida inicialización
# dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true # exe y dlls -> 155mb
# dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false # exe y dll -> 900kb

# Standalone
# dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true # exe -> 158mb
# dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true # exe -> 158mb
# dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true # exe -> 70mb

# Framework-dependent
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true # exe -> 800kb

if ($LASTEXITCODE -eq 0) {
    Write-Host "Publicación completada exitosamente." -ForegroundColor Green
} else {
    Write-Host "Error en la publicación." -ForegroundColor Red
}
