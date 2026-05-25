$ErrorActionPreference = 'SilentlyContinue'
$docs = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)
$root = Join-Path $docs 'My Games\Terraria\tModLoader\ModSources\NoxusBoss'
$out = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) 'PsychoOverlay\noxus_scan_results.txt'

"DOCS=$docs" | Set-Content -Path $out -Encoding UTF8
"ROOT=$root" | Add-Content -Path $out -Encoding UTF8
"ROOT_EXISTS=$(Test-Path -LiteralPath $root)" | Add-Content -Path $out -Encoding UTF8
"" | Add-Content -Path $out -Encoding UTF8
"== AGENTS.md files ==" | Add-Content -Path $out -Encoding UTF8
Get-ChildItem -LiteralPath $root -Filter AGENTS.md -Recurse -Force | Select-Object -ExpandProperty FullName | Add-Content -Path $out -Encoding UTF8

"" | Add-Content -Path $out -Encoding UTF8
"== Root items ==" | Add-Content -Path $out -Encoding UTF8
Get-ChildItem -LiteralPath $root -Force | Select-Object Mode,Length,LastWriteTime,Name | Format-Table -AutoSize | Out-String -Width 220 | Add-Content -Path $out -Encoding UTF8

"" | Add-Content -Path $out -Encoding UTF8
"== Filename matches ==" | Add-Content -Path $out -Encoding UTF8
$patterns = 'Nilk|Milk|Psycho|Psychedelic|Shader|Screen|Distort|Wavy|Blotch|Cosmos|Void|Galaxy|Nameless|Drug|Halluc|Trip|Lactose|Debuff'
Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.FullName -match $patterns } | Select-Object -First 250 FullName | Format-Table -AutoSize | Out-String -Width 260 | Add-Content -Path $out -Encoding UTF8

"" | Add-Content -Path $out -Encoding UTF8
"== Code text matches ==" | Add-Content -Path $out -Encoding UTF8
$files = Get-ChildItem -LiteralPath $root -Recurse -File -Include *.cs,*.fx,*.hlsl,*.glsl,*.json,*.hjson,*.txt,*.lang,*.resx
$matches = $files | Select-String -Pattern 'Nilk|NILK|Psychedelic|photosensitivity|disorienting|Boss health|0 HP|ScreenShader|Distort|Chromatic|NamelessDeity|WavyBlotch|Void|Cosmos|Galaxy' -Context 2,2
$matches | Select-Object -First 220 | ForEach-Object {
    "FILE: $($_.Path)"; "LINE: $($_.LineNumber)"; "TEXT: $($_.Line.Trim())"; if ($_.Context.PreContext) { "PRE: " + ($_.Context.PreContext -join ' | ') }; if ($_.Context.PostContext) { "POST: " + ($_.Context.PostContext -join ' | ') }; "---"
} | Add-Content -Path $out -Encoding UTF8

"" | Add-Content -Path $out -Encoding UTF8
"== Selected asset dimensions if available ==" | Add-Content -Path $out -Encoding UTF8
Add-Type -AssemblyName System.Drawing
Get-ChildItem -LiteralPath $root -Recurse -File -Include *.png | Where-Object { $_.FullName -match 'Psychedelic|Void|Cosmos|WavyBlotch|Galaxy|NamelessDeitySky|BackgroundPattern|Nilk' } | Select-Object -First 120 | ForEach-Object {
    try {
        $img = [System.Drawing.Image]::FromFile($_.FullName)
        "$($_.FullName) :: $($img.Width)x$($img.Height)"
        $img.Dispose()
    } catch {
        "$($_.FullName) :: dimensions unavailable"
    }
} | Add-Content -Path $out -Encoding UTF8

"SCAN_DONE" | Add-Content -Path $out -Encoding UTF8
Write-Host "Wrote $out"
