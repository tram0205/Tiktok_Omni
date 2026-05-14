$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "Form1.cs"
$all = Get-Content $path -Encoding UTF8

# Full BuildAffiliateHunterUi ends ~1384 (includes all Controls.Add + closing brace).
$ranges = @(
    @(507, 1194),
    @(1196, 1384),
    @(1625, 2070),
    @(2072, 2154),
    @(3311, 3553),
    @(3555, 3607)
)

$extractSet = @{}
foreach ($r in $ranges) {
    for ($i = $r[0]; $i -le $r[1]; $i++) {
        $extractSet[$i] = $true
    }
}

$extracted = New-Object System.Collections.Generic.List[string]
foreach ($r in $ranges) {
    for ($i = $r[0]; $i -le $r[1]; $i++) {
        [void]$extracted.Add($all[$i - 1])
    }
}

$header = @"
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace tiktok_Omni
{
    public partial class Form1 : Form
    {
"@

$footer = @"
    }
}
"@

$uiPath = Join-Path $root "Form1.Ui.cs"
[System.IO.File]::WriteAllText($uiPath, $header + [Environment]::NewLine + ($extracted -join [Environment]::NewLine) + [Environment]::NewLine + $footer)

$newLines = New-Object System.Collections.Generic.List[string]
for ($idx = 1; $idx -le $all.Count; $idx++) {
    if (-not $extractSet.ContainsKey($idx)) {
        [void]$newLines.Add($all[$idx - 1])
    }
}

$text = ($newLines -join [Environment]::NewLine)
$text = [regex]::Replace($text, "public class Form1 : Form", "public partial class Form1 : Form", 1)
[System.IO.File]::WriteAllText($path, $text)

Write-Host "Form1.Ui.cs: $($extracted.Count) lines extracted"
Write-Host "Form1.cs: $($newLines.Count) lines remaining"
