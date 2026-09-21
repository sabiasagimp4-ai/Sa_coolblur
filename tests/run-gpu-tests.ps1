$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual C++ tools not found' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
New-Item -ItemType Directory -Force artifacts | Out-Null
$command = "`"$vcvars`" && cl /nologo /EHsc /std:c++17 /O2 tests\gpu_tests.cpp /Fo:artifacts\gpu_tests.obj /Fe:artifacts\gpu_tests.exe /link d3d11.lib d3dcompiler.lib"
& cmd /c $command
if ($LASTEXITCODE -ne 0) { throw 'GPU test compilation failed' }
& ./artifacts/gpu_tests.exe
if ($LASTEXITCODE -ne 0) { throw 'GPU tests failed' }
