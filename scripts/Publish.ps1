# 构建 Windows x64 自包含单文件，并生成本地分发包；不会上传任何内容。
[CmdletBinding()]
param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$projectFile = Join-Path $projectRoot 'Source/ScreenGlass.csproj'
[xml]$project = Get-Content -LiteralPath $projectFile
$version = $project.Project.PropertyGroup.Version
$bundleName = "ScreenGlass-$version-win-x64"
$publishPath = Join-Path $projectRoot "Source/work/package/$bundleName"
New-Item -ItemType Directory -Path (Join-Path $projectRoot "dist") -Force | Out-Null
New-Item -ItemType Directory -Path $publishPath -Force | Out-Null
# 将托管程序、桌面运行库和原生依赖一起封装；WPF 不进行裁剪。
& $Dotnet publish $projectFile -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -p:PublishTrimmed=false -o $publishPath --nologo
if ($LASTEXITCODE -ne 0) { throw '独立运行包编译失败。' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/使用说明.txt') -Destination $publishPath -Force
# 从实际使用的运行库包提取许可，避免说明与分发内容不一致。
$assets = Get-Content -LiteralPath (Join-Path $projectRoot 'Source/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageRoot = @($assets.packageFolders.PSObject.Properties)[0].Name
$notices = Get-Content -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.txt') -Raw
$runtimePackages = $assets.project.frameworks.PSObject.Properties | ForEach-Object { $_.Value.downloadDependencies } | Where-Object { $_.name -match '^Microsoft\.(NETCore|WindowsDesktop)\.App\.Runtime\.win-x64$' }
if (@($runtimePackages).Count -ne 2) { throw '未找到两个自包含运行库的许可来源。' }
foreach ($package in $runtimePackages) {
    $runtimeVersion = $package.version.Trim('[',']').Split(',')[0].Trim()
    $packagePath = Join-Path $packageRoot "$($package.name.ToLowerInvariant())/$runtimeVersion"
    $licenses = Get-ChildItem -LiteralPath $packagePath -File | Where-Object { $_.Name -match '^LICENSE(\.TXT)?$|^THIRD-PARTY-NOTICES\.TXT$' }
    if (-not $licenses) { throw "缺少运行库许可：$($package.name)" }
    foreach ($license in $licenses) {
        $notices += "`n`n===== $($package.name) / $runtimeVersion / $($license.Name) =====`n"
        $notices += Get-Content -LiteralPath $license.FullName -Raw
    }
}
$notices | Set-Content -LiteralPath (Join-Path $publishPath 'THIRD-PARTY-NOTICES.txt') -Encoding utf8
# 只打包应用和说明，排除开发配置与用户偏好。
$zipPath = Join-Path $projectRoot "dist/$bundleName.zip"
Compress-Archive -LiteralPath (Join-Path $publishPath 'ScreenGlass.exe'), `
    (Join-Path $publishPath '使用说明.txt'), (Join-Path $publishPath 'THIRD-PARTY-NOTICES.txt') `
    -DestinationPath $zipPath -Force
# 对本次生成的分发包生成一次校验值，供下载用户核对。
$checksum = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
"$($checksum.Hash.ToLowerInvariant())  $bundleName.zip" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ascii
# 生成干净源码包：只包含项目源码、资源与发布说明，不包含用户配置和开发缓存。
$sourceStage = Join-Path $projectRoot 'Source/work/source-package'
# 删除前核实解析后的目标就是当前项目的固定源码暂存目录。
if (Test-Path -LiteralPath $sourceStage) {
    $resolvedSourceStage = (Resolve-Path -LiteralPath $sourceStage).Path
    if ($resolvedSourceStage -ne [IO.Path]::GetFullPath((Join-Path $projectRoot 'Source/work/source-package'))) { throw '源码暂存目录不在预期位置。' }
    Remove-Item -LiteralPath $resolvedSourceStage -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $sourceStage 'Source') -Force | Out-Null
foreach ($item in 'README.md','CHANGELOG.md','THIRD-PARTY-NOTICES.txt','global.json','.gitignore','scripts','docs','.github') {
    Copy-Item -LiteralPath (Join-Path $projectRoot $item) -Destination $sourceStage -Recurse -Force
}
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Source') -File | Where-Object Extension -in '.cs','.xaml','.csproj','.manifest' | Copy-Item -Destination (Join-Path $sourceStage 'Source') -Force
foreach ($item in 'Assets','Controls') { Copy-Item -LiteralPath (Join-Path $projectRoot "Source/$item") -Destination (Join-Path $sourceStage 'Source') -Recurse -Force }
$sourceZip = Join-Path $projectRoot "dist/ScreenGlass-$version-source.zip"
if (Test-Path -LiteralPath $sourceZip) { Remove-Item -LiteralPath $sourceZip }
[System.IO.Compression.ZipFile]::CreateFromDirectory($sourceStage, $sourceZip)
# 更新唯一的本地运行副本。旧进程持有的映射只暂存到工作目录，不作为回退程序。
$livePath = Join-Path $projectRoot 'App'
New-Item -ItemType Directory -Path $livePath -Force | Out-Null
$liveExe = Join-Path $livePath 'ScreenGlass.exe'
$nextExe = Join-Path $livePath 'ScreenGlass.next.exe'
Copy-Item -LiteralPath (Join-Path $publishPath 'ScreenGlass.exe') -Destination $nextExe -Force
if (Test-Path -LiteralPath $liveExe) {
    $retiredFile = Join-Path $projectRoot ("Source/work/retired-" + [Guid]::NewGuid().ToString('N') + '.exe')
    Move-Item -LiteralPath $liveExe -Destination $retiredFile
}
Move-Item -LiteralPath $nextExe -Destination $liveExe
foreach ($name in '使用说明.txt','THIRD-PARTY-NOTICES.txt') { Copy-Item -LiteralPath (Join-Path $publishPath $name) -Destination $livePath -Force }
if ($retiredFile -and (Test-Path -LiteralPath $retiredFile)) {
    try { Remove-Item -LiteralPath $retiredFile -Force -ErrorAction Stop } catch { Write-Output '旧实例仍占用映射，请从托盘退出旧版后打开新版。' }
}
# 删除暂存目录前核实它们确实位于当前项目的固定工作区。
foreach ($stage in @($publishPath, $sourceStage)) {
    $resolvedStage = (Resolve-Path -LiteralPath $stage).Path
    $expectedStage = [IO.Path]::GetFullPath($stage)
    if ($resolvedStage -ne $expectedStage -or -not $resolvedStage.StartsWith([IO.Path]::GetFullPath((Join-Path $projectRoot 'Source/work')) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '暂存目录不在预期位置。' }
    Remove-Item -LiteralPath $resolvedStage -Recurse -Force
}
Write-Output "本地程序：$liveExe"
Write-Output "本地发布包：$zipPath"
Write-Output "干净源码包：$sourceZip"
