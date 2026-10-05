using System;
using System.Diagnostics;

namespace BangumiMerge;

public static class Utils
{
    public static int StartProcess(string exe, IEnumerable<string> arguments, CancellationToken? ctx = null)
    {
        var psi = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };

        var process = new Process
        {
            StartInfo = psi
        };

        if (ctx != null)
        {
            ctx.Value.Register(() =>
            {
                Console.WriteLine($"Killing {exe}...");
                process.Kill();
            });
        }

        Console.WriteLine($"Launching process:\n{exe} '{string.Join("' '", psi.ArgumentList)}'");

        process.Start();
        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(-1);
        Console.WriteLine($"{exe} exited with code {process.ExitCode}");
        return process.ExitCode;
    }
}