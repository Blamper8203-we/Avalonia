# Odblokowanie DLL/EXE po buildzie (Windows "pochodzi z innego komputera")
param([string]$OutputPath, [string]$AssemblyName)
$dll = Join-Path $OutputPath "$AssemblyName.dll"
$exe = Join-Path $OutputPath "$AssemblyName.exe"
if (Test-Path $dll) { Unblock-File -LiteralPath $dll }
if (Test-Path $exe) { Unblock-File -LiteralPath $exe }
