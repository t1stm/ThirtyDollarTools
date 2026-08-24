<#
.SYNOPSIS
    Start Menu shortcuts for the visualizer, plus the .tdwproj (editor) and
    .tdw/.moai/.🗿 (visualizer) file types, for the current user. No admin rights:
    everything goes under HKCU and %APPDATA%.

.PARAMETER AppDirectory
    The folder holding ThirtyDollarVisualizer.exe. Defaults to the folder this script
    sits in, then the repo's Release output.

.PARAMETER Uninstall
    Removes the shortcuts and the file types again.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\Install-Shortcuts.ps1 "C:\Program Files\Thirty Dollar Visualizer"
#>
[CmdletBinding()]
param(
    [string] $AppDirectory,
    [switch] $Uninstall
)

$ErrorActionPreference = 'Stop'

$here      = Split-Path -Parent $MyInvocation.MyCommand.Path
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$projectId  = 'ThirtyDollarVisualizer.Project'
$sequenceId = 'ThirtyDollarVisualizer.Sequence'
# .🗿 is a surrogate pair. The registry is UTF-16 and takes it; Linux's glob
# matcher does not, which is why only this half of the pair of installers gets it.
$sequenceExtensions = @('.tdw', '.moai', ".$([char]0xD83D)$([char]0xDDFF)")
$shortcuts = @(
    (Join-Path $startMenu 'Thirty Dollar Visualizer.lnk'),
    (Join-Path $startMenu 'Thirty Dollar Editor.lnk')
)

# Explorer caches file types until something tells it not to; SHCNE_ASSOCCHANGED is
# that something, and saves a sign-out after installing.
Add-Type -Namespace Shell -Name Notify -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("shell32.dll")]
public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
'@
function Update-Explorer { [Shell.Notify]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero) }

if ($Uninstall) {
    $shortcuts | Where-Object { Test-Path $_ } | ForEach-Object { Remove-Item $_ -Force; "Removed $_" }
    $keys = @("HKCU:\Software\Classes\$projectId", "HKCU:\Software\Classes\$sequenceId",
              'HKCU:\Software\Classes\.tdwproj') +
            ($sequenceExtensions | ForEach-Object { "HKCU:\Software\Classes\$_" })
    foreach ($key in $keys) {
        if (Test-Path -LiteralPath $key) { Remove-Item -LiteralPath $key -Recurse -Force; "Removed $key" }
    }
    Update-Explorer
    'Removed.'
    return
}

if (-not $AppDirectory) {
    foreach ($candidate in @($here, (Join-Path $here '..\..\..\Visualizer\ThirtyDollarVisualizer\bin\Release\net10.0'))) {
        if (Test-Path (Join-Path $candidate 'ThirtyDollarVisualizer.exe')) { $AppDirectory = $candidate; break }
    }
}
if (-not $AppDirectory) { throw 'Pass the folder holding ThirtyDollarVisualizer.exe.' }

$AppDirectory = (Resolve-Path $AppDirectory).Path
$exe = Join-Path $AppDirectory 'ThirtyDollarVisualizer.exe'
if (-not (Test-Path $exe)) { throw "No ThirtyDollarVisualizer.exe in $AppDirectory" }

# The icon lives next to the exe so the shortcuts and the file type keep working when
# this folder is deleted or the repo moves.
$icon = Join-Path $AppDirectory 'thirty-dollar-visualizer.ico'
Copy-Item (Join-Path $here 'thirty-dollar-visualizer.ico') $icon -Force

# WorkingDirectory / the command's working folder are the app's own: settings and
# Editor Backups are written relative to it, and a Start Menu launch starts elsewhere.
$shell = New-Object -ComObject WScript.Shell
function New-Shortcut($path, $arguments, $description) {
    $link = $shell.CreateShortcut($path)
    $link.TargetPath       = $exe
    $link.Arguments        = $arguments
    $link.WorkingDirectory = $AppDirectory
    $link.IconLocation     = $icon
    $link.Description      = $description
    $link.Save()
    "Wrote $path"
}

New-Shortcut $shortcuts[0] '' 'Visualizer and editor for Thirty Dollar Website sequences'
New-Shortcut $shortcuts[1] '--mode editor' 'Thirty Dollar Editor'

# One ProgID per thing you can open: a project opens the editor, a sequence plays in
# the visualizer.
function Register-FileType($progId, $description, $command, $extensions) {
    New-Item -LiteralPath "HKCU:\Software\Classes\$progId" -Force | Out-Null
    Set-ItemProperty -LiteralPath "HKCU:\Software\Classes\$progId" -Name '(default)' -Value $description

    New-Item -LiteralPath "HKCU:\Software\Classes\$progId\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -LiteralPath "HKCU:\Software\Classes\$progId\DefaultIcon" -Name '(default)' -Value "`"$icon`""

    New-Item -LiteralPath "HKCU:\Software\Classes\$progId\shell\open\command" -Force | Out-Null
    Set-ItemProperty -LiteralPath "HKCU:\Software\Classes\$progId\shell\open\command" -Name '(default)' `
        -Value "`"$exe`" $command `"%1`""

    foreach ($extension in $extensions) {
        New-Item -LiteralPath "HKCU:\Software\Classes\$extension" -Force | Out-Null
        Set-ItemProperty -LiteralPath "HKCU:\Software\Classes\$extension" -Name '(default)' -Value $progId
        "Registered $extension -> $progId"
    }
}

Register-FileType $projectId  'Thirty Dollar Editor project' '--mode editor -i'     @('.tdwproj')
Register-FileType $sequenceId 'Thirty Dollar sequence'       '--mode visualizer -i' $sequenceExtensions

Update-Explorer
"Installed, pointing at $exe"
