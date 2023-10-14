using System;
using System.Diagnostics;

namespace BangumiMerge;

public static class Utils
{
    public static int StartProcess(string exe, string arguments)
    {
        Console.WriteLine($"Launching {exe} with arguments: \n{arguments}");
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            }
        };
        process.Start();
        process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(-1);
        Console.WriteLine($"{exe} exited with code {process.ExitCode}");
        return process.ExitCode;
    }

    public static string Quote(this string s)
    {
        return "\"" + s + "\"";
    }
}