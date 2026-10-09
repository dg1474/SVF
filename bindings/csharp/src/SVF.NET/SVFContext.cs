using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

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
            _wpaExecutablePath = customWpaPath ?? ResolveNativeBinary("wpa.exe");
            _extapiPath = customExtapiPath ?? ResolveNativeBinary("extapi.bc");
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

            var psi = new ProcessStartInfo
            {
                FileName = _wpaExecutablePath,
                Arguments = string.Join(" ", args),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

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

        private static List<IndirectCallTarget> ParseIndirectCallTargets(string output)
        {
            var list = new List<IndirectCallTarget>();
            if (string.IsNullOrEmpty(output)) return list;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("->") && (line.Contains("CallSite") || line.Contains("indcall") || line.Contains("target")))
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

        private static string ResolveNativeBinary(string binaryName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string runtimesNative = Path.Combine(baseDir, "runtimes", "win-x64", "native", binaryName);
            if (File.Exists(runtimesNative)) return runtimesNative;

            string directPath = Path.Combine(baseDir, binaryName);
            if (File.Exists(directPath)) return directPath;

            string svfHome = Environment.GetEnvironmentVariable("SVF_DIR") ?? string.Empty;
            if (!string.IsNullOrEmpty(svfHome))
            {
                string inBin = Path.Combine(svfHome, "Release-build", "bin", binaryName);
                if (File.Exists(inBin)) return inBin;
                string inLib = Path.Combine(svfHome, "Release-build", "lib", binaryName);
                if (File.Exists(inLib)) return inLib;
            }

            return binaryName;
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
