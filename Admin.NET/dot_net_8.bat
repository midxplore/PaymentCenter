@echo off
setlocal
set "BASE=%~dp0"

for /r "%BASE%" %%F in (*.csproj) do (
    powershell -NoProfile -ExecutionPolicy Bypass -Command ^
        "$p = '%%F'; $c = [IO.File]::ReadAllText($p, [Text.UTF8Encoding]::new($false)); $c2 = [regex]::Replace($c, '(?is)<TargetFrameworks>\s*.*?\s*</TargetFrameworks>', '<TargetFrameworks>net8.0</TargetFrameworks>'); if ($c2 -ne $c) { [IO.File]::WriteAllText($p, $c2, [Text.UTF8Encoding]::new($true)); Write-Host ('Updated: ' + $p) }"
)
endlocal
pause
