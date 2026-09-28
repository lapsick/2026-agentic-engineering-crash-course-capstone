<#
.SYNOPSIS
    Dot-source to get Get-WorkingTreeSnapshot: the git tree (and a dangling commit on top of HEAD)
    of the current working tree — tracked changes plus untracked, non-ignored files — built in a
    throwaway index, so neither the working tree, the real index, nor any ref is touched.

    Tree   = content fingerprint (same files -> same hash), used by speckit-gate.ps1 -ReuseVerdict.
    Commit = something `git worktree add` can check out, used by run-evals.ps1 -WorkingTree.

.EXAMPLE
    . "$PSScriptRoot/working-tree-snapshot.ps1"
    (Get-WorkingTreeSnapshot -Exclude 'reviews').Tree
#>
function Get-WorkingTreeSnapshot([string[]]$Exclude = @()) {
    $index = [IO.Path]::GetTempFileName()
    $previous = $env:GIT_INDEX_FILE
    try {
        Copy-Item (git rev-parse --git-path index) $index -Force
        $env:GIT_INDEX_FILE = $index
        $pathspec = @('.') + @($Exclude | ForEach-Object { ":(exclude)$_" })
        git add -A -- @pathspec 2>&1 | Out-Null
        $tree = "$(git write-tree)".Trim()
    }
    finally {
        $env:GIT_INDEX_FILE = $previous
        Remove-Item $index -Force -ErrorAction SilentlyContinue
    }
    $commit = "$(git commit-tree $tree -p HEAD -m 'working-tree snapshot')".Trim()
    return [pscustomobject]@{ Tree = $tree; Commit = $commit }
}
