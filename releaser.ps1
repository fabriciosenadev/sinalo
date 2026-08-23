param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$NextPatch,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $projectRoot "src\Sinalo.App\Sinalo.App.csproj"
$updaterProject = Join-Path $projectRoot "src\Sinalo.Updater\Sinalo.Updater.csproj"
$coverageScript = Join-Path $projectRoot "eng\test-coverage.ps1"
$releaseRoot = Join-Path $projectRoot ".release"
$publishDirectory = Join-Path $releaseRoot "Sinalo-$Runtime"
$installerDirectory = Join-Path $releaseRoot "installer"
$installerScript = Join-Path $projectRoot "installer\Sinalo.iss"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Assert-Version {
    param([string]$Value)

    if ($Value -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
        throw "A versao '$Value' e invalida. Use major.minor.patch ou major.minor.patch.build."
    }
}

function Get-ProjectVersion {
    [xml]$project = Get-Content -LiteralPath $appProject
    $version = $project.Project.PropertyGroup.Version | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "A propriedade <Version> nao foi encontrada em $appProject."
    }
    Assert-Version -Value $version
    return $version
}

function Set-ProjectVersion {
    param([string]$Value)

    $content = [System.IO.File]::ReadAllText($appProject)
    $updated = [regex]::Replace($content, '(?<=<Version>)[^<]+(?=</Version>)', $Value, 1)
    if ($updated -eq $content) {
        throw "A propriedade <Version> nao foi encontrada em $appProject."
    }
    [System.IO.File]::WriteAllText($appProject, $updated)
}

function Get-NextPatchVersion {
    param([string]$CurrentVersion)

    $parts = $CurrentVersion.Split('.')
    return "{0}.{1}.{2}" -f $parts[0], $parts[1], ([int]$parts[2] + 1)
}

function Get-InnoCompiler {
    $command = Get-Command iscc -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ($NextPatch -and -not [string]::IsNullOrWhiteSpace($Version)) {
    throw "Use -NextPatch ou -Version, mas nao os dois juntos."
}

 $shouldPersistNextPatch = $false
if ($NextPatch) {
    $Version = Get-NextPatchVersion -CurrentVersion (Get-ProjectVersion)
    $shouldPersistNextPatch = $true
    Write-Step "Proxima versao calculada: $Version"
}
elseif ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-ProjectVersion
}

Assert-Version -Value $Version

if (-not $SkipTests) {
    Write-Step "Executando testes e validando cobertura"
    & $coverageScript -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Testes ou cobertura falharam. O instalador nao foi gerado."
    }
}

Write-Step "Publicando Sinalo $Version para $Runtime"
New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
Remove-Item -LiteralPath $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

& dotnet publish $appProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    --output $publishDirectory `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Falha ao publicar o Sinalo."
}

Write-Step "Publicando atualizador"
$updaterDirectory = Join-Path $publishDirectory "updater"
& dotnet publish $updaterProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    --output $updaterDirectory `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Falha ao publicar o atualizador."
}

$executablePath = Join-Path $publishDirectory "Sinalo.App.exe"
if (-not (Test-Path -LiteralPath $executablePath)) {
    throw "O executavel publicado nao foi encontrado: $executablePath"
}

$innoCompiler = Get-InnoCompiler
if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
    throw "Inno Setup 6 nao encontrado. Instale-o ou adicione ISCC.exe ao PATH. O publish foi gerado em: $publishDirectory"
}

Write-Step "Compilando instalador"
New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null
& $innoCompiler "/DMyAppVersion=$Version" "/DMySourceDir=$publishDirectory" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao compilar o instalador."
}

$installerPath = Join-Path $installerDirectory "Sinalo-Setup-win-x64.exe"
if (-not (Test-Path -LiteralPath $installerPath)) {
    throw "O instalador nao foi encontrado: $installerPath"
}

$checksumPath = "$installerPath.sha256"
"$(Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath | Select-Object -ExpandProperty Hash)  $(Split-Path -Leaf $installerPath)" | Set-Content -LiteralPath $checksumPath -NoNewline

if ($shouldPersistNextPatch) {
    Set-ProjectVersion -Value $Version
}

Write-Step "Release finalizada"
Write-Host "Publish:    $publishDirectory" -ForegroundColor Green
Write-Host "Instalador: $installerPath" -ForegroundColor Green
Write-Host "Checksum:   $checksumPath" -ForegroundColor Green
