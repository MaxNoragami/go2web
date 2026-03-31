$ErrorActionPreference = "Stop"

$binaryName = "go2web.exe"
$installDir = [System.IO.Path]::Combine($env:LOCALAPPDATA, "Programs", "go2web")

Write-Host "Installing $binaryName..."

# Create directory if it doesn't exist
if (-not (Test-Path -Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir | Out-Null
}

# Copy the executable
$sourcePath = Join-Path -Path $PWD -ChildPath $binaryName
if (-not (Test-Path -Path $sourcePath)) {
    Write-Error "Could not find $binaryName in the current directory."
    exit 1
}

Copy-Item -Path $sourcePath -Destination (Join-Path -Path $installDir -ChildPath $binaryName) -Force

Write-Host "Successfully copied $binaryName to $installDir"

# Update PATH
$userPath = [Environment]::GetEnvironmentVariable("PATH", [EnvironmentVariableTarget]::User)
if ($userPath -match [regex]::Escape($installDir)) {
    Write-Host "$installDir is already in your PATH."
} else {
    Write-Host "Adding $installDir to your PATH..."
    $newPath = "$userPath;$installDir"
    [Environment]::SetEnvironmentVariable("PATH", $newPath, [EnvironmentVariableTarget]::User)
    
    # Broadcast change to prompt user to restart terminal
    Write-Host "PATH updated successfully. You MUST RESTART your PowerShell window for the changes to take effect."
}

Write-Host "Installation complete! Open a new PowerShell window and type 'go2web -h'"