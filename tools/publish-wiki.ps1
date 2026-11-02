# Publish the curated wiki pages to the GitHub wiki repository.
#
# Usage:
#   pwsh -File tools/publish-wiki.ps1
#   pwsh -File tools/publish-wiki.ps1 -Message "Update FAQ" -NoPush
#
# The wiki is a separate Git repository. This script keeps the version-controlled
# pages under wiki/ as the source of truth, then mirrors them into 5DO.wiki.git.

[CmdletBinding()]
param(
    [string]$Message = "Update 5DO wiki",
    [string]$WikiRemote = "https://github.com/purelogiccode/5DO.wiki.git",
    [switch]$NoPush
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sourceDir = Join-Path $repoRoot 'wiki'

if (-not (Test-Path $sourceDir)) {
    throw "Wiki source directory not found: $sourceDir"
}

$sourcePages = Get-ChildItem -Path $sourceDir -Filter '*.md' -File
if ($sourcePages.Count -eq 0) {
    throw "No Markdown pages found in $sourceDir"
}

$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ("5do-wiki-" + [Guid]::NewGuid().ToString('N'))
try {
    Write-Host "Cloning $WikiRemote"
    git clone --quiet $WikiRemote $workDir
    if ($LASTEXITCODE -ne 0) {
        throw "git clone failed. Check GitHub authentication and wiki access."
    }

    # Remove tracked pages that are no longer in wiki/ so the published wiki mirrors
    # the source directory. Keep .git and any non-Markdown wiki assets.
    Get-ChildItem -Path $workDir -Filter '*.md' -File | Remove-Item -Force
    foreach ($page in $sourcePages) {
        Copy-Item -Path $page.FullName -Destination (Join-Path $workDir $page.Name) -Force
    }

    git -C $workDir add --all
    if ($LASTEXITCODE -ne 0) {
        throw "git add failed."
    }

    git -C $workDir diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Wiki is already up to date."
        return
    }

    git -C $workDir -c user.name="5DO documentation" -c user.email="docs@users.noreply.github.com" commit --quiet -m $Message
    if ($LASTEXITCODE -ne 0) {
        throw "git commit failed."
    }

    if ($NoPush) {
        Write-Host "Committed wiki changes in $workDir (not pushed)."
        return
    }

    Write-Host "Pushing wiki changes"
    git -C $workDir push --quiet origin master
    if ($LASTEXITCODE -ne 0) {
        throw "git push failed. Check GitHub authentication."
    }

    Write-Host "Wiki published: https://github.com/purelogiccode/5DO/wiki"
}
finally {
    if (Test-Path $workDir) {
        Remove-Item -Path $workDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
