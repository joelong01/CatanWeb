<#
.SYNOPSIS
    Encodes image files as base64 and embeds them into the companion JSON documents.
    Run this whenever player images change. After running, the JSON files are complete
    CosmosDB documents ready to insert verbatim.

.DESCRIPTION
    For each *.json file in this directory:
    1. Reads the "name" field from the JSON
    2. Finds a matching image file (case-insensitive: <name>.jpg, <name>.jpeg, or <name>.png)
    3. Base64-encodes the image and writes it into the "imageData" field
    4. Sets "imageContentType" from the file extension
    5. Writes the updated JSON back to disk

.EXAMPLE
    pwsh ./encode-images.ps1
#>

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

$jsonFiles = Get-ChildItem -Path $scriptDir -Filter "*.json"
if ($jsonFiles.Count -eq 0) {
    Write-Host "No JSON files found in $scriptDir"
    exit 0
}

$imageExtensions = @(".jpg", ".jpeg", ".png")
$mimeTypes = @{
    ".jpg"  = "image/jpeg"
    ".jpeg" = "image/jpeg"
    ".png"  = "image/png"
}

foreach ($jsonFile in $jsonFiles) {
    $doc = Get-Content -Raw $jsonFile.FullName | ConvertFrom-Json
    $name = $doc.name
    if (-not $name) {
        Write-Host "SKIP $($jsonFile.Name): no 'name' field"
        continue
    }

    # Find matching image file by player name (case-insensitive)
    $imageFile = $null
    foreach ($ext in $imageExtensions) {
        $candidate = Get-ChildItem -Path $scriptDir -Filter "$name$ext" -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ieq "$name$ext" } |
            Select-Object -First 1
        if ($candidate) {
            $imageFile = $candidate
            break
        }
    }

    if (-not $imageFile) {
        Write-Host "SKIP $($jsonFile.Name): no image found for name '$name'"
        continue
    }

    # Base64 encode the image
    $bytes = [System.IO.File]::ReadAllBytes($imageFile.FullName)
    $base64 = [Convert]::ToBase64String($bytes)
    $contentType = $mimeTypes[$imageFile.Extension.ToLower()]

    # Update the document
    $doc.imageData = $base64
    $doc.imageContentType = $contentType

    # Write back with consistent formatting
    $doc | ConvertTo-Json -Depth 10 | Set-Content -Path $jsonFile.FullName -Encoding UTF8
    Write-Host "OK $($jsonFile.Name): embedded $($imageFile.Name) ($contentType, $($bytes.Length) bytes)"
}

Write-Host ""
Write-Host "Done. JSON files now contain embedded image data."
