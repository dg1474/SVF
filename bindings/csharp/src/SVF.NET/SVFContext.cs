using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("SVF.NET.Tests")]

namespace SVF.NET
{
    /// <summary>
    /// Context for configuring and executing SVF static analysis operations.
    /// </summary>
    public class SVFContext : IDisposable
    {
        private string _wpaExecutablePath;
        private string _extapiPath;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SVFContext"/> class, automatically discovering native SVF binaries.
        /// </summary>
        /// <param name="customWpaPath">Optional path to wpa executable.</param>
        /// <param name="customExtapiPath">Optional path to extapi.bc bitcode model.</param>
        public SVFContext(string? customWpaPath = null, string? customExtapiPath = null)
        {
            string wpaBinaryName = GetPlatformBinaryName("wpa");
            _wpaExecutablePath = customWpaPath ?? ResolveNativeBinary(wpaBinaryName);
            _extapiPath = customExtapiPath ?? ResolveNativeBinary("extapi.bc");

            EnsureExecutablePermissions(_wpaExecutablePath);
        }

        /// <summary>
        /// Gets the resolved path to the WPA executable.
        /// </summary>
        public string WpaPath => _wpaExecutablePath;

        /// <summary>
        /// Gets the resolved path to the extapi.bc bitcode file.
        /// </summary>
        public string ExtApiPath => _extapiPath;

        /// <summary>
        /// Returns the platform-specific executable name (e.g. wpa.exe on Windows, wpa on Unix).
        /// </summary>
        /// <param name="baseName">Base executable name without extension.</param>
        /// <returns>Platform-specific binary name.</returns>
        public static string GetPlatformBinaryName(string baseName)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? baseName + ".exe"
                : baseName;
        }

        /// <summary>
        /// Resolves the current runtime identifier (RID) for native binary packaging.
        /// </summary>
        /// <returns>RID string such as win-x64, linux-x64, or osx-arm64.</returns>
        public static string GetCurrentRuntimeIdentifier()
        {
            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                Architecture.X86 => "x86",
                _ => "x64"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win-" + arch;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "osx-" + arch;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux-" + arch;

            return "win-x64";
        }

        /// <summary>
        /// Runs Andersen's inclusion-based pointer analysis on the specified LLVM bitcode file.
        /// </summary>
        /// <param name="bitcodePath">Path to the .bc or .ll bitcode file.</param>
        /// <param name="resolveIndirectCalls">If true, resolves indirect function call targets (-print-fp).</param>
        /// <param name="extraArguments">Optional extra CLI arguments passed to wpa.</param>
        /// <returns>An <see cref="AndersenResult"/> containing analysis results.</returns>
        public AndersenResult RunAndersen(string bitcodePath, bool resolveIndirectCalls = true, string? extraArguments = null)
        {
            if (!File.Exists(bitcodePath))
                throw new FileNotFoundException($"Bitcode file not found: {bitcodePath}");

            if (!File.Exists(_wpaExecutablePath))
                throw new FileNotFoundException($"SVF wpa executable not found at: {_wpaExecutablePath}");

            var psi = new ProcessStartInfo
            {
                FileName = _wpaExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

#if NET8_0_OR_GREATER
            psi.ArgumentList.Add("-ander");
            if (resolveIndirectCalls)
            {
                psi.ArgumentList.Add("-print-fp");
            }
            if (File.Exists(_extapiPath))
            {
                psi.ArgumentList.Add("-extapi");
                psi.ArgumentList.Add(_extapiPath);
            }
            if (!string.IsNullOrWhiteSpace(extraArguments))
            {
                foreach (var extra in extraArguments!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    psi.ArgumentList.Add(extra);
                }
            }
            psi.ArgumentList.Add(bitcodePath);
#else
            var args = new List<string> { "-ander" };
            if (resolveIndirectCalls)
            {
                args.Add("-print-fp");
            }
            if (File.Exists(_extapiPath))
            {
                args.Add($"-extapi \"{_extapiPath}\"");
            }
            if (!string.IsNullOrWhiteSpace(extraArguments))
            {
                args.Add(extraArguments!);
            }
            args.Add($"\"{bitcodePath}\"");
            psi.Arguments = string.Join(" ", args);
#endif

            using var process = Process.Start(psi) 
                ?? throw new InvalidOperationException("Failed to start wpa process.");

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var result = new AndersenResult
            {
                Success = (process.ExitCode == 0),
                RawOutput = stdout + (string.IsNullOrEmpty(stderr) ? "" : "\n" + stderr),
                IndirectCallTargets = ParseIndirectCallTargets(stdout)
            };

            return result;
        }

        internal static List<IndirectCallTarget> ParseIndirectCallTargets(string output)
        {
            var list = new List<IndirectCallTarget>();
            if (string.IsNullOrEmpty(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("->") && 
                    (line.IndexOf("callsite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     line.IndexOf("call", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     line.IndexOf("target", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var parts = line.Split(new[] { "->" }, StringSplitOptions.None);
                    if (parts.Length == 2)
                    {
                        var callSite = parts[0].Trim();
                        var targets = new List<string>();
                        foreach (var target in parts[1].Split(new[] { ',', '{', '}', '[', ']', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            targets.Add(target.Trim());
                        }
                        list.Add(new IndirectCallTarget { CallSite = callSite, Targets = targets });
                    }
                }
            }
            return list;
        }

        internal static string ResolveNativeBinary(string binaryName, string? baseDir = null, string? svfDir = null, string? pathEnv = null)
        {
            baseDir ??= AppDomain.CurrentDomain.BaseDirectory;
            string rid = GetCurrentRuntimeIdentifier();

            // 1. Check packaged runtimes/{rid}/native/{binaryName}
            string ridPath = Path.Combine(baseDir, "runtimes", rid, "native", binaryName);
            if (File.Exists(ridPath)) return ridPath;

            // 2. Check direct path in application base directory
            string directPath = Path.Combine(baseDir, binaryName);
            if (File.Exists(directPath)) return directPath;

            // 3. Check SVF_DIR environment variable
            string svfHome = svfDir ?? Environment.GetEnvironmentVariable("SVF_DIR") ?? string.Empty;
            if (!string.IsNullOrEmpty(svfHome))
            {
                string[] candidateDirs = {
                    Path.Combine(svfHome, "Release-build", "bin"),
                    Path.Combine(svfHome, "Debug-build", "bin"),
                    Path.Combine(svfHome, "bin"),
                    Path.Combine(svfHome, "Release-build", "lib"),
                    Path.Combine(svfHome, "Debug-build", "lib"),
                    Path.Combine(svfHome, "lib")
                };
                foreach (var dir in candidateDirs)
                {
                    string p = Path.Combine(dir, binaryName);
                    if (File.Exists(p)) return p;
                }
            }

            // 4. Search in system PATH directories
            string envPath = pathEnv ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (!string.IsNullOrEmpty(envPath))
            {
                char separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
                foreach (var dir in envPath.Split(separator))
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string p = Path.Combine(dir.Trim(), binaryName);
                    if (File.Exists(p)) return p;
                }
            }

            return binaryName;
        }

        internal static void EnsureExecutablePermissions(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

#if NET8_0_OR_GREATER
            try
            {
                var mode = File.GetUnixFileMode(filePath);
                var execBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                if ((mode & execBits) != execBits)
                {
                    File.SetUnixFileMode(filePath, mode | execBits);
                    return;
                }
            }
            catch
            {
                // Fall back to chmod subprocess if UnixFileMode fails
            }
#endif

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = "+x \"" + filePath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit();
            }
            catch
            {
                // Silently ignore if chmod is unavailable or fails
            }
        }

        /// <summary>
        /// Releases managed and unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }
}
