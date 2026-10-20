using System;
using System.IO;
using System.Runtime.InteropServices;
using SVF.NET;

namespace SVF.NET.Tests
{
    class Program
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;

        static int Main(string[] args)
        {
            string rid = SVFContext.GetCurrentRuntimeIdentifier();
            Console.WriteLine("==================================================");
            Console.WriteLine($" SVF.NET Cross-Platform Test Suite");
            Console.WriteLine($" Runtime Identifier : {rid}");
            Console.WriteLine($" Operating System   : {RuntimeInformation.OSDescription}");
            Console.WriteLine($" Architecture       : {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($" .NET Version       : {Environment.Version}");
            Console.WriteLine("==================================================");

            RunTest("Runtime Identification", TestRuntimeDetection);
            RunTest("Platform Binary Naming", TestPlatformBinaryNaming);
            RunTest("Native Binary Resolution Cascade", TestResolveNativeBinaryPriority);
            RunTest("Multi-Platform RID Layout Simulation", TestMultiPlatformLayoutSimulation);
            RunTest("Indirect Call Target Parsing", TestIndirectCallParsing);
            RunTest("Executable Permission Handling", TestExecutablePermissions);
            RunTest("Error Handling & Input Validation", TestErrorHandling);
            RunTest("End-to-End Analysis Execution", () => TestEndToEndAnalysis(args));

            Console.WriteLine("==================================================");
            Console.WriteLine($" Test Summary: {_passedCount} passed, {_failedCount} failed.");
            Console.WriteLine("==================================================");

            return (_failedCount == 0) ? 0 : 1;
        }

        private static void RunTest(string testName, Action testAction)
        {
            Console.Write($"[RUN]  {testName}... ");
            try
            {
                testAction();
                _passedCount++;
                Console.WriteLine("PASS");
            }
            catch (Exception ex)
            {
                _failedCount++;
                Console.WriteLine("FAIL");
                Console.WriteLine($"       Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"       Inner: {ex.InnerException.Message}");
                }
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Assertion failed: {message}");
            }
        }

        private static void TestRuntimeDetection()
        {
            string rid = SVFContext.GetCurrentRuntimeIdentifier();
            Assert(!string.IsNullOrEmpty(rid), "RID should not be empty");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert(rid.StartsWith("win-"), $"Expected Windows RID to start with 'win-', got: {rid}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert(rid.StartsWith("linux-"), $"Expected Linux RID to start with 'linux-', got: {rid}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Assert(rid.StartsWith("osx-"), $"Expected macOS RID to start with 'osx-', got: {rid}");
            }
        }

        private static void TestPlatformBinaryNaming()
        {
            string wpaName = SVFContext.GetPlatformBinaryName("wpa");
            string saberName = SVFContext.GetPlatformBinaryName("saber");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert(wpaName == "wpa.exe", $"Expected 'wpa.exe' on Windows, got: {wpaName}");
                Assert(saberName == "saber.exe", $"Expected 'saber.exe' on Windows, got: {saberName}");
            }
            else
            {
                Assert(wpaName == "wpa", $"Expected 'wpa' on Unix/macOS, got: {wpaName}");
                Assert(saberName == "saber", $"Expected 'saber' on Unix/macOS, got: {saberName}");
            }
        }

        private static void TestResolveNativeBinaryPriority()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "svf_test_resolve_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string rid = SVFContext.GetCurrentRuntimeIdentifier();
                string binaryName = "test_bin_" + Guid.NewGuid().ToString("N");

                // Priority 1: runtimes/{rid}/native/{binaryName}
                string ridNativeDir = Path.Combine(tempDir, "runtimes", rid, "native");
                Directory.CreateDirectory(ridNativeDir);
                string ridFilePath = Path.Combine(ridNativeDir, binaryName);
                File.WriteAllText(ridFilePath, "mock");

                string resolved1 = SVFContext.ResolveNativeBinary(binaryName, baseDir: tempDir);
                Assert(resolved1 == ridFilePath, $"Priority 1 failed: expected {ridFilePath}, got {resolved1}");

                File.Delete(ridFilePath);

                // Priority 2: baseDir/{binaryName}
                string baseFilePath = Path.Combine(tempDir, binaryName);
                File.WriteAllText(baseFilePath, "mock");

                string resolved2 = SVFContext.ResolveNativeBinary(binaryName, baseDir: tempDir);
                Assert(resolved2 == baseFilePath, $"Priority 2 failed: expected {baseFilePath}, got {resolved2}");

                File.Delete(baseFilePath);

                // Priority 3: SVF_DIR/Release-build/bin/{binaryName}
                string svfDir = Path.Combine(tempDir, "mock_svf");
                string svfBinDir = Path.Combine(svfDir, "Release-build", "bin");
                Directory.CreateDirectory(svfBinDir);
                string svfFilePath = Path.Combine(svfBinDir, binaryName);
                File.WriteAllText(svfFilePath, "mock");

                string resolved3 = SVFContext.ResolveNativeBinary(binaryName, baseDir: tempDir, svfDir: svfDir);
                Assert(resolved3 == svfFilePath, $"Priority 3 failed: expected {svfFilePath}, got {resolved3}");

                // Priority 4: PATH/{binaryName}
                string mockPathDir = Path.Combine(tempDir, "mock_path");
                Directory.CreateDirectory(mockPathDir);
                string pathFilePath = Path.Combine(mockPathDir, binaryName);
                File.WriteAllText(pathFilePath, "mock");

                string resolved4 = SVFContext.ResolveNativeBinary(binaryName, baseDir: tempDir, svfDir: "", pathEnv: mockPathDir);
                Assert(resolved4 == pathFilePath, $"Priority 4 failed: expected {pathFilePath}, got {resolved4}");

                // Fallback: returns binaryName when not found
                string fallback = SVFContext.ResolveNativeBinary("non_existent_binary", baseDir: tempDir, svfDir: "", pathEnv: "");
                Assert(fallback == "non_existent_binary", $"Fallback failed: expected 'non_existent_binary', got {fallback}");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static void TestMultiPlatformLayoutSimulation()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "svf_multi_rid_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string[] rids = { "win-x64", "linux-x64", "osx-arm64", "osx-x64" };
                foreach (var rid in rids)
                {
                    string binaryName = rid.StartsWith("win-") ? "wpa.exe" : "wpa";
                    string nativeDir = Path.Combine(tempDir, "runtimes", rid, "native");
                    Directory.CreateDirectory(nativeDir);
                    string stagedPath = Path.Combine(nativeDir, binaryName);
                    File.WriteAllText(stagedPath, "mock binary for " + rid);

                    Assert(File.Exists(stagedPath), $"Staged file for {rid} should exist");
                }

                Assert(File.Exists(Path.Combine(tempDir, "runtimes", "osx-arm64", "native", "wpa")), "macOS ARM64 binary must exist");
                Assert(File.Exists(Path.Combine(tempDir, "runtimes", "osx-x64", "native", "wpa")), "macOS x64 binary must exist");
                Assert(File.Exists(Path.Combine(tempDir, "runtimes", "linux-x64", "native", "wpa")), "Linux x64 binary must exist");
                Assert(File.Exists(Path.Combine(tempDir, "runtimes", "win-x64", "native", "wpa.exe")), "Windows x64 binary must exist");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static void TestIndirectCallParsing()
        {
            string mockOutput = 
                "*********General Stats***************\n" +
                "IndCallSites        2\n" +
                "CallSite: %call1 = call i32 %fp1() -> { foo, bar, baz }\n" +
                "IndCall in main at line 42 -> [ handler_a, handler_b ]\n" +
                "Regular line without arrows\n";

            var targets = SVFContext.ParseIndirectCallTargets(mockOutput);
            Assert(targets.Count == 2, $"Expected 2 indirect call targets, got {targets.Count}");

            Assert(targets[0].Targets.Count == 3, $"Expected 3 targets in first callsite, got {targets[0].Targets.Count}");
            Assert(targets[0].Targets.Contains("foo"), "Targets should contain 'foo'");
            Assert(targets[0].Targets.Contains("bar"), "Targets should contain 'bar'");
            Assert(targets[0].Targets.Contains("baz"), "Targets should contain 'baz'");

            Assert(targets[1].Targets.Count == 2, $"Expected 2 targets in second callsite, got {targets[1].Targets.Count}");
            Assert(targets[1].Targets.Contains("handler_a"), "Targets should contain 'handler_a'");
            Assert(targets[1].Targets.Contains("handler_b"), "Targets should contain 'handler_b'");
        }

        private static void TestExecutablePermissions()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "#!/bin/sh\necho ok\n");
                SVFContext.EnsureExecutablePermissions(tempFile);
                Assert(File.Exists(tempFile), "Temp file should still exist after permission check");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static void TestErrorHandling()
        {
            using var ctx = new SVFContext();

            bool threwMissingBitcode = false;
            try
            {
                ctx.RunAndersen("non_existent_bitcode_file.bc");
            }
            catch (FileNotFoundException)
            {
                threwMissingBitcode = true;
            }
            Assert(threwMissingBitcode, "RunAndersen must throw FileNotFoundException when bitcode file is missing");

            string tempBc = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempBc, "mock bc");
                using var invalidCtx = new SVFContext(customWpaPath: "non_existent_wpa_path_xyz");
                bool threwMissingWpa = false;
                try
                {
                    invalidCtx.RunAndersen(tempBc);
                }
                catch (FileNotFoundException)
                {
                    threwMissingWpa = true;
                }
                Assert(threwMissingWpa, "RunAndersen must throw FileNotFoundException when wpa executable is missing");
            }
            finally
            {
                if (File.Exists(tempBc))
                {
                    File.Delete(tempBc);
                }
            }
        }

        private static void TestEndToEndAnalysis(string[] args)
        {
            string currentDir = Directory.GetCurrentDirectory();
            string wpaBinary = SVFContext.GetPlatformBinaryName("wpa");
            string wpaPath = Path.Combine(currentDir, "Release-build", "bin", wpaBinary);
            string extapiPath = Path.Combine(currentDir, "Release-build", "lib", "extapi.bc");

            string bitcodePath = args.Length > 0 && File.Exists(args[0])
                ? args[0]
                : (File.Exists(Path.Combine(currentDir, "Test-Suite", "test_cases_bc", "basic_c_tests", "struct-nested-array3.c.bc"))
                    ? Path.Combine(currentDir, "Test-Suite", "test_cases_bc", "basic_c_tests", "struct-nested-array3.c.bc")
                    : Path.Combine(currentDir, "test_fp.bc"));

            using var ctx = File.Exists(wpaPath)
                ? new SVFContext(wpaPath, extapiPath)
                : new SVFContext();

            if (!File.Exists(ctx.WpaPath))
            {
                Console.Write(" [SKIPPED: Native wpa binary not in current environment] ");
                return;
            }

            if (!File.Exists(bitcodePath))
            {
                Console.Write(" [SKIPPED: Bitcode test file not found in current environment] ");
                return;
            }

            var result = ctx.RunAndersen(bitcodePath, resolveIndirectCalls: true);
            Assert(result.Success, "Andersen analysis process execution failed (non-zero exit code)");
            Assert(!string.IsNullOrEmpty(result.RawOutput), "Andersen analysis output should not be empty");
            Assert(result.RawOutput.Contains("General Stats") || result.RawOutput.Contains("AddrsNum"), 
                "Output should contain analysis statistics");
        }
    }
}
