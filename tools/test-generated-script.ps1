param(
    [Parameter(Mandatory = $true)][string]$GameBin,
    [Parameter(Mandatory = $true)][string]$ScriptPath,
    [ValidateRange(1, 100000)][int]$MaxCharacters = 100000,
    [string]$BaselinePath,
    [string]$OutputAssemblyPath,
    [switch]$Integration
)
# Windows PowerShell 5.1: compile the packaged body as C# 6 with SDK Roslyn.
# This checks name resolution after packaging, not the game's runtime whitelist.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Use Windows PowerShell 5.1.' }
$framework = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$referencePaths = @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { Join-Path $framework $_ }
foreach ($name in @('Sandbox.Common', 'Sandbox.Game', 'Sandbox.Graphics', 'SpaceEngineers.Game',
    'SpaceEngineers.ObjectBuilders', 'VRage', 'VRage.Game', 'VRage.Library', 'VRage.Math',
    'VRage.Scripting', 'System.Collections.Immutable', 'ProtoBuf.Net.Core', 'netstandard')) {
    $referencePaths += Join-Path $GameBin ($name + '.dll')
}
$body = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $ScriptPath).Path)
if ($body.Length -gt $MaxCharacters) {
    throw "Generated PB body has $($body.Length) characters; budget is $MaxCharacters."
}
Write-Output "SIZE: $($body.Length) / $MaxCharacters characters; PB capacity remaining: $(100000 - $body.Length)."
if ($BaselinePath) {
    $baseline = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $BaselinePath).Path)
    Write-Output "SIZE: baseline $($baseline.Length); reduction $($baseline.Length - $body.Length) characters."
}
if ($body -match '[^\x00-\x7F]') {
    throw 'The generated PB script must contain ASCII comments and text only.'
}
# The setup guide must precede the first Program member, including fields.
if (!$Integration -and $body -notmatch '(?s)\A\s*/\*\s*PLAYER SETUP.*?\*/\s*(?:public|private|protected|internal|readonly|static|const|string|bool|int|long|double|float)\b') {
    throw 'The generated PB script must start with the player setup instructions.'
}
$prefix = @'
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
namespace PackagedScriptValidation
{
    public sealed class Program : MyGridProgram
    {
'@
$source = $prefix + "`n" + $body + "`n    }`n}`n"
$outputDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) '.local/generated-validation'
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$stem = Join-Path $outputDirectory ([Guid]::NewGuid().ToString('N'))
$outputAssembly = $stem + '.dll'
if ($OutputAssemblyPath) {
    $outputAssembly = [IO.Path]::GetFullPath($OutputAssemblyPath)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($outputAssembly)) | Out-Null
}
# Compile only; loading a PB Program outside the game would require runtime initialization.
# Desktop Add-Type uses a C# 5 compiler and cannot validate the supported C# 6 syntax.
$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the active .NET SDK.' }
$sdkEntries = @(& dotnet --list-sdks)
if ($LASTEXITCODE -ne 0) { throw 'Cannot list .NET SDKs.' }
$sdkEntry = @($sdkEntries | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + ' \[') })
if ($sdkEntry.Count -ne 1) { throw 'Cannot locate the active SDK compiler.' }
$sdkRoot = [regex]::Match($sdkEntry[0], '\[(.+)\]$').Groups[1].Value
$compiler = Join-Path $sdkRoot ($sdkVersion + '/Roslyn/bincore/csc.dll')
$sourcePath = $stem + '.cs'
[IO.File]::WriteAllText($sourcePath, $source, (New-Object Text.UTF8Encoding($false)))
$compilerArguments = @('exec', $compiler, '/nologo', '/noconfig', '/nostdlib+', '/langversion:6',
    '/target:library', ('/out:' + $outputAssembly))
$compilerArguments += @($referencePaths | ForEach-Object { '/reference:' + $_ })
$compilerArguments += $sourcePath
& dotnet @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Packaged PB body failed C# 6 compilation.' }
Write-Output 'PASS: packaged PB body compiles with game references and template imports (without System.Globalization import).'
if (!$Integration) { Write-Output 'PASS: generated script starts with player setup instructions.' }
Write-Output 'PASS: generated script contains ASCII text only.'
Write-Output 'NOT CHECKED: in-game whitelist enforcement, compilation and execution.'
