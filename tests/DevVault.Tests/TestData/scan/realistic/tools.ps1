#Requires -Version 5.1
<#
.SYNOPSIS
  Gets a thing.
.DESCRIPTION
  Longer text with } and { braces.
#>
function Get-Thing {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [int]$Count = 1
    )
    $items = @{ a = 1; b = 2 }
    foreach ($k in $items.Keys) {
        Write-Output "key: $k {$($items[$k])}"
    }
    if ($Count -gt 1) {
        Write-Host 'many }'
    }
}

# Sets a thing
function Set-Thing($Name, $Value) {
    $h = @"
Here-string with "quotes" and { brace }
"@
    Write-Output $h
    Write-Output $Name
}

filter Only-Even {
    if ($_ % 2 -eq 0) { $_ }
    else { $null }
    return
}

function Invoke-Work {
    param([scriptblock]$Block)
    try {
        & $Block
    }
    catch {
        Write-Error "failed: $_"
    }
    finally {
        Write-Verbose 'done'
    }
}

class Person {
    [string]$Name
    Person([string]$n) {
        $this.Name = $n
        Write-Host "created"
    }
    [string] Greet() {
        return "Hi $($this.Name)"
    }
}

function Last-One {
    $x = 1
    $y = 2
    $x + $y
}
