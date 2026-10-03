# The Java downloader is truncated on this machine. Seed the official Wrapper cache
# with a PowerShell download, but enforce exactly the same official SHA-256.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $env:GRADLE_USER_HOME) { $env:GRADLE_USER_HOME = "$root\work\gradle-home" }
$properties = Get-Content "$PSScriptRoot\gradle\wrapper\gradle-wrapper.properties" -Raw
$url = [regex]::Match($properties, '(?m)^distributionUrl=(.+)$').Groups[1].Value.Trim().Replace('\:', ':')
$sha = [regex]::Match($properties, '(?m)^distributionSha256Sum=(.+)$').Groups[1].Value.Trim()
# Gradle's cache key is the unsigned MD5 of distributionUrl encoded in base 36.
$md5 = [Security.Cryptography.MD5]::Create()
try { [byte[]]$bytes = $md5.ComputeHash([Text.Encoding]::UTF8.GetBytes($url)) } finally { $md5.Dispose() }
[Array]::Reverse($bytes)
[byte[]]$positive = $bytes + [byte]0
$number = [System.Numerics.BigInteger]::new($positive)
$key = ''
while ($number -gt 0) {
    $remainder = [System.Numerics.BigInteger]::Zero
    $number = [System.Numerics.BigInteger]::DivRem($number, 36, [ref]$remainder)
    $key = '0123456789abcdefghijklmnopqrstuvwxyz'[[int]$remainder] + $key
}
$name = [IO.Path]::GetFileName(([Uri]$url).AbsolutePath)
$cache = Join-Path $env:GRADLE_USER_HOME ("wrapper\dists\" + [IO.Path]::GetFileNameWithoutExtension($name) + "\$key")
if (Test-Path -LiteralPath (Join-Path $cache "$name.ok")) { return }
$download = Join-Path $root "work\$name"
New-Item -ItemType Directory -Path "$root\work",$cache -Force | Out-Null
if (-not (Test-Path $download) -or (Get-FileHash $download -Algorithm SHA256).Hash.ToLower() -ne $sha) {
    Invoke-WebRequest -Uri $url -OutFile $download
}
if ((Get-FileHash $download -Algorithm SHA256).Hash.ToLower() -ne $sha) { throw 'Gradle SHA-256 mismatch; download rejected' }
Copy-Item -LiteralPath $download -Destination (Join-Path $cache $name) -Force
