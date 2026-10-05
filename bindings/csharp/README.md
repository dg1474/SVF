# SVF.NET - C# / .NET Bindings and Native Runtime for SVF

`SVF.NET` provides .NET bindings and packaged native binaries for **SVF** (Static Value-Flow Analysis Framework).

## Installation

Install via NuGet:

```bash
dotnet add package SVF.NET
```

## Quick Start Example

```csharp
using System;
using SVF.NET;

class Program
{
    static void Main(string[] args)
    {
        // 1. Initialize context (automatically discovers native wpa.exe and extapi.bc)
        using var ctx = new SVFContext();

        Console.WriteLine($"Using WPA at: {ctx.WpaPath}");

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

## Features

- **Cross-Platform .NET Support:** Targets `net8.0`, `net9.0`, and `netstandard2.0`.
- **Self-Contained Native Binaries:** Pre-bundled with high-performance, statically linked (`/MT`) Windows MSVC binaries (`wpa.exe`, `extapi.bc`).
- **Indirect Call Resolution:** Easily extract and analyze function pointer targets.
