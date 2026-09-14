# 虚空宝箱 - Thunderstore 发布打包脚本
#
# 用法：powershell -ExecutionPolicy Bypass -File .\package.ps1
#
# 产物：dist\VoidChest-<版本>.zip
#   结构（参考 ValheimModding-YamlDotNet）：
#     manifest.json
#     README.md
#     CHANGELOG.md
#     LICENSE.txt
#     icon.png
#     BepInEx/plugins/VoidChest/VoidChest.dll

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$projectDir = Join-Path $root "VoidChest"
$projectFile = Join-Path $projectDir "VoidChest.csproj"
$distDir = Join-Path $root "dist"
$stagingDir = Join-Path $distDir "staging"

Write-Host "==> 构建 ($Configuration)..."
dotnet build $projectFile -c $Configuration -v q
if ($LASTEXITCODE -ne 0) {
    throw "构建失败"
}

$manifest = Get-Content (Join-Path $projectDir "manifest.json") -Raw | ConvertFrom-Json
$version = $manifest.version_number
$zipPath = Join-Path $distDir ("VoidChest-{0}.zip" -f $version)

Write-Host "==> 组装发布包 (v$version)..."
if (Test-Path -LiteralPath $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir | Out-Null

# 包根文件
Copy-Item (Join-Path $projectDir "manifest.json") $stagingDir
Copy-Item (Join-Path $projectDir "CHANGELOG.md") $stagingDir
Copy-Item (Join-Path $projectDir "LICENSE.txt") $stagingDir
Copy-Item (Join-Path $projectDir "icon.png") $stagingDir
Copy-Item (Join-Path $root "README.md") $stagingDir

# 插件文件
$pluginTarget = Join-Path $stagingDir "BepInEx\plugins\VoidChest"
New-Item -ItemType Directory -Path $pluginTarget -Force | Out-Null
Copy-Item (Join-Path $projectDir "obj\$Configuration\VoidChest.dll") $pluginTarget

Write-Host "==> 压缩..."
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $zipPath
Remove-Item -LiteralPath $stagingDir -Recurse -Force

Write-Host "==> 完成: $zipPath"
Write-Host ""
Write-Host "包内容:"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$zip.Entries | ForEach-Object { Write-Host ("  " + $_.FullName) }
$zip.Dispose()
