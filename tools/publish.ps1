# usb'ye atılacak tek parça exe'yi üretir (publish/WorkTracker.exe)
# kullanım:  powershell -ExecutionPolicy Bypass -File tools\publish.ps1
param([string]$Output = (Join-Path $PSScriptRoot '..\publish'))

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\WorkTracker\WorkTracker.csproj'

# self-contained: iş bilgisayarında .net kurulu olmasa da çalışsın diye runtime exe'nin içinde.
# boyutu büyütüyor (~65 MB) ama kurulum gerektirmiyor
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $Output

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Get-Item (Join-Path $Output 'WorkTracker.exe')
Write-Output ("hazir: {0}  ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB))
