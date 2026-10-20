# SVF.NET - C# / .NET Bindings and Native Runtime for SVF

[![NuGet Version](https://img.shields.io/nuget/v/SVF.NET.svg?style=flat-square)](https://www.nuget.org/packages/SVF.NET)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SVF.NET.svg?style=flat-square)](https://www.nuget.org/packages/SVF.NET)
[![License: AGPL-3.0-or-later](https://img.shields.io/badge/License-AGPL--3.0--or--later-blue.svg?style=flat-square)](https://licenses.nuget.org/AGPL-3.0-or-later)

`SVF.NET` provides managed .NET bindings and packaged native binaries for **[SVF](https://github.com/SVF-tools/SVF)** (Static Value-Flow Analysis Framework for Source Code and LLVM Bitcode).

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package SVF.NET
```

## Quick Start Example

### 1. Compile Source Code to LLVM Bitcode
Generate the `.bc` bitcode file using Clang:
```bash
clang -emit-llvm -c program.c -o program.bc
```

### 2. Run Analysis in C#
```csharp
using System;
using SVF.NET;

class Program
{
    static void Main(string[] args)
    {
        // 1. Initialize context (automatically discovers native wpa/wpa.exe and extapi.bc)
        using var ctx = new SVFContext();

        Console.WriteLine($"Using SVF engine at: {ctx.WpaPath}");

        // 2. Run Andersen pointer analysis on LLVM bitcode
        var result = ctx.RunAndersen("program.bc", resolveIndirectCalls: true);

        if (result.Success)
        {
            Console.WriteLine("Analysis completed successfully!");
            foreach (var target in result.IndirectCallTargets)
            {
                Console.WriteLine(target);
            }
        }
        else
        {
            Console.WriteLine("Analysis failed: " + result.RawOutput);
        }
    }
}
```

## Features & Platform Support

- **Target Frameworks:** Compatible with .NET 8, .NET 9, .NET 10, and .NET Standard 2.0 (`net8.0`, `netstandard2.0`).
- **Multi-RID Architecture Support:**
  - `win-x64`: Pre-bundled with statically linked (`/MT`) binaries (`wpa.exe`, `saber.exe`, `extapi.bc`).
  - `linux-x64`: Discovers binaries staged in `runtimes/linux-x64/native/wpa`, `$SVF_DIR`, or system `$PATH`. Sets executable permissions automatically.
  - `osx-arm64` / `osx-x64`: Discovers Apple Silicon or Intel macOS binaries staged in `runtimes/osx-*/native/wpa`, `$SVF_DIR`, or system `$PATH`.
- **Indirect Call Resolution:** Extract and analyze function pointer targets via `-print-fp`.

## Testing Environments

Cross-platform test scripts are available in `bindings/csharp/tests`:

- **Windows:** Run `.\run-tests-windows.ps1` (tests against local native `wpa.exe`).
- **Linux (Docker):** Run `.\run-tests-linux.ps1` (validates runtime detection and execution under Debian/Ubuntu with .NET 8 and .NET 9).
- **Linux (Native):** Run `./run-tests-linux.sh`.
- **GitHub Actions (Multi-OS):** Automated CI matrix workflow (`.github/workflows/csharp-multios-test.yml`) running across `windows-latest`, `ubuntu-latest`, `macos-latest` (Apple Silicon), and `macos-13` (Intel) with .NET 8 and 9.

## License

`SVF.NET` is licensed under the [GNU Affero General Public License v3 or later (AGPL-3.0-or-later)](https://licenses.nuget.org/AGPL-3.0-or-later), matching upstream SVF.
