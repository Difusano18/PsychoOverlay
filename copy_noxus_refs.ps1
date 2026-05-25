$ErrorActionPreference = 'Stop'
$docs = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
$root = Join-Path $docs 'My Games\Terraria\tModLoader\ModSources\NoxusBoss'
$proj = Join-Path $desktop 'PsychoOverlay'
$ref = Join-Path $proj 'NoxusReferences'
New-Item -ItemType Directory -Force -Path $ref | Out-Null

$shaderRel = @(
  'Assets\AutoloadedEffects\Filters\NilkScreenDistortionShader.fx',
  'Assets\AutoloadedEffects\Filters\PsychedelicShader.fx',
  'Assets\AutoloadedEffects\Filters\General\ChromaticAberrationShader.fx',
  'Assets\AutoloadedEffects\Filters\ScreenDistortionShader.fx'
)
foreach ($rel in $shaderRel) {
  $src = Join-Path $root $rel
  if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $ref ([IO.Path]::GetFileName($src))) -Force }
}

# Copy visual assets used by current overlay if present.
$assetRels = @(
  'Assets\Textures\Skies\NamelessDeity\NamelessDeitySky.png',
  'Assets\Textures\Skies\NamelessDeity\BackgroundPattern.png',
  'Assets\Textures\Extra\Psychedelic.png',
  'Assets\Textures\Extra\Void.png',
  'Assets\Textures\Extra\Cosmos.png',
  'Assets\Textures\Extra\Noise\WavyBlotchNoiseDetailed.png'
)
for ($i = 1; $i -le 9; $i++) { $assetRels += "Assets\Textures\Skies\NamelessDeity\Galaxy$i.png" }
foreach ($rel in $assetRels) {
  $src = Join-Path $root $rel
  if (Test-Path -LiteralPath $src) {
    $dst = Join-Path $proj $rel
    New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($dst)) | Out-Null
    Copy-Item -LiteralPath $src -Destination $dst -Force
  }
}

$out = Join-Path $proj 'noxus_targeted_results.txt'
"ROOT=$root" | Set-Content -Path $out -Encoding UTF8
"COPIED_REFERENCES=$ref" | Add-Content -Path $out -Encoding UTF8
"" | Add-Content -Path $out -Encoding UTF8
"== Exact code references ==" | Add-Content -Path $out -Encoding UTF8
$files = Get-ChildItem -LiteralPath $root -Recurse -File -Include *.cs,*.fx,*.hlsl,*.json,*.hjson,*.txt,*.resx
$matches = $files | Select-String -Pattern 'NilkScreenDistortionShader|PsychedelicShader|Nilk|NILK|NilkDebuff|photosensitivity|The Nilk|disorienting' -Context 2,3
$matches | Select-Object -First 180 | ForEach-Object {
    "FILE: $($_.Path)"; "LINE: $($_.LineNumber)"; "TEXT: $($_.Line.Trim())"; if ($_.Context.PreContext) { "PRE: " + ($_.Context.PreContext -join ' | ') }; if ($_.Context.PostContext) { "POST: " + ($_.Context.PostContext -join ' | ') }; "---"
} | Add-Content -Path $out -Encoding UTF8

"" | Add-Content -Path $out -Encoding UTF8
"== Project copied assets ==" | Add-Content -Path $out -Encoding UTF8
Get-ChildItem -LiteralPath (Join-Path $proj 'Assets') -Recurse -File -ErrorAction SilentlyContinue | Select-Object FullName,Length | Format-Table -AutoSize | Out-String -Width 220 | Add-Content -Path $out -Encoding UTF8
Write-Host "Wrote $out"
