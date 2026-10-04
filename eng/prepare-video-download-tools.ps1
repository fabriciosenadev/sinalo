param([switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $root '.release\tools-cache'
$destination = Join-Path $root 'src\Sinalo.App\binaries\video-download'
New-Item -ItemType Directory -Force -Path $cache, $destination | Out-Null

$packages = @(
    @{ Name = 'yt-dlp.exe'; Url = 'https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe'; Hash = '66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a'; Exe = 'yt-dlp.exe'; ExeHash = '66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a' },
    @{ Name = 'deno-x86_64-pc-windows-msvc.zip'; Url = 'https://github.com/denoland/deno/releases/download/v2.9.7/deno-x86_64-pc-windows-msvc.zip'; Hash = 'a0c3101b4158d1dfb7d6a78a7bf0f3de80c96bb423c152beec8beb22786f2238'; Exe = 'deno.exe'; ExeHash = 'e020f3e232bd16e33768dee528e5983349c962952051ced0a5d58ad42f5d9b33' },
    @{ Name = 'ffmpeg-N-127149-g50d206a75b-win64-gpl.zip'; Url = 'https://github.com/yt-dlp/FFmpeg-Builds/releases/download/autobuild-2026-10-03-19-23/ffmpeg-N-127149-g50d206a75b-win64-gpl.zip'; Hash = '33f215a02689d5563a495ea58c694f4354fb04a89e5d2eef8ab76b3daec89ef1'; Exe = 'ffmpeg.exe'; ExeHash = '8c1893db532e412c868277042f8d3752b8f38dd074e8643eb821fa4b9bdeb41b' }
)

foreach ($package in $packages) {
    $target = Join-Path $destination $package.Exe
    if ((Test-Path -LiteralPath $target) -and -not $Force) {
        $existingHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash.ToLowerInvariant()
        if ($existingHash -eq $package.ExeHash) { continue }
    }
    $archivePath = Join-Path $cache $package.Name
    if (-not (Test-Path -LiteralPath $archivePath)) {
        Write-Host "Baixando $($package.Name)..."
        Invoke-WebRequest -Uri $package.Url -OutFile $archivePath -TimeoutSec 1800
    }
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    if ($hash -ne $package.Hash) { throw "Checksum inválido para $($package.Name). O arquivo não será instalado." }
    if ($package.Name.EndsWith('.exe')) {
        Copy-Item -LiteralPath $archivePath -Destination $target -Force
        continue
    }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -eq $package.Exe -or $_.FullName.EndsWith("/$($package.Exe)") } | Select-Object -First 1
        if ($null -eq $entry) { throw "Executável $($package.Exe) ausente em $($package.Name)." }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
    }
    finally { $archive.Dispose() }
    $executableHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $target).Hash.ToLowerInvariant()
    if ($executableHash -ne $package.ExeHash) { throw "Executável $($package.Exe) não corresponde ao checksum esperado." }
}

$ffmpegArchive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $cache 'ffmpeg-N-127149-g50d206a75b-win64-gpl.zip'))
try {
    $license = $ffmpegArchive.Entries | Where-Object { $_.FullName.EndsWith('/LICENSE.txt') } | Select-Object -First 1
    if ($null -eq $license) { throw 'Licença do build FFmpeg ausente.' }
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($license, (Join-Path $destination 'FFmpeg-LICENSE.txt'), $true)
}
finally { $ffmpegArchive.Dispose() }

$notices = @(
    @{ Name = 'yt-dlp-THIRD_PARTY_LICENSES.txt'; Url = 'https://raw.githubusercontent.com/yt-dlp/yt-dlp/2026.08.19/THIRD_PARTY_LICENSES.txt' },
    @{ Name = 'Deno-LICENSE.md'; Url = 'https://raw.githubusercontent.com/denoland/deno/v2.9.7/LICENSE.md' }
)
foreach ($notice in $notices) {
    $path = Join-Path $destination $notice.Name
    if (-not (Test-Path -LiteralPath $path)) { Invoke-WebRequest -Uri $notice.Url -OutFile $path -TimeoutSec 60 }
}

foreach ($name in @('yt-dlp.exe', 'deno.exe', 'ffmpeg.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $destination $name) -PathType Leaf)) { throw "Dependência ausente: $name" }
}
Write-Host 'Componentes de download prontos.'
