using System;
using System.IO;
using SVF.NET;

namespace SVF.NET.Tests
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" Testing SVF.NET C# Integration on Windows (MSVC)");
            Console.WriteLine("==================================================");

            string currentDir = Directory.GetCurrentDirectory();
            string wpaPath = Path.Combine(currentDir, "Release-build", "bin", "wpa.exe");
            string extapiPath = Path.Combine(currentDir, "Release-build", "lib", "extapi.bc");
            string bitcodePath = Path.Combine(currentDir, "test_fp.bc");

            Console.WriteLine($"[1] WPA Executable: {wpaPath} (Exists: {File.Exists(wpaPath)})");
            Console.WriteLine($"[2] ExtAPI Module:  {extapiPath} (Exists: {File.Exists(extapiPath)})");
            Console.WriteLine($"[3] Test Bitcode:   {bitcodePath} (Exists: {File.Exists(bitcodePath)})");

            if (!File.Exists(wpaPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: wpa.exe not found.");
                Console.ResetColor();
                return 1;
            }

            using var ctx = new SVFContext(wpaPath, extapiPath);

            Console.WriteLine("\n[4] Running Andersen's Pointer Analysis with Indirect Call Resolution from C#...");
            var result = ctx.RunAndersen(bitcodePath, resolveIndirectCalls: true);

            Console.WriteLine($"\n[5] Execution Success: {result.Success}");
            Console.WriteLine($"[6] Raw Analysis Output Preview:");
            var lines = result.RawOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int count = Math.Min(20, lines.Length);
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine($"    {lines[i]}");
            }

            if (result.Success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n==================================================");
                Console.WriteLine(" SVF.NET C# Integration Test PASSED successfully!");
                Console.WriteLine("==================================================");
                Console.ResetColor();
                return 0;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nTest FAILED.");
                Console.ResetColor();
                return 1;
            }
        }
    }
}
