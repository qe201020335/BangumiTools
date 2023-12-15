using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BangumiMerge;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "BangumiMerge";

// get output folder
var outPathEnvVar = Environment.GetEnvironmentVariable("BANGUMIMERGE_OUTPUT_PATH");
string outputPath;
if (!string.IsNullOrWhiteSpace(outPathEnvVar) && Directory.Exists(outPathEnvVar))
{
    outputPath = Path.GetFullPath(outPathEnvVar);
}
else
{
    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
    {
        outputPath = Path.GetFullPath(@"Z:/Video");
    }
    else
    {
        Console.WriteLine("Output folder not specified using BANGUMIMERGE_OUTPUT_PATH or not exists.");
        Environment.Exit(-1);
        return;
    }
}

var inputFiles = args;
bool CopyModifiedTime = true;
bool CopyFileName = true;


var cSource = new CancellationTokenSource();
var cToken = cSource.Token;

Console.CancelKeyPress += delegate { cSource.Cancel(); };


if (inputFiles.Length == 0)
{
    Console.WriteLine("Drag and drop files to this exe to merge them.");
    Console.WriteLine("Press Enter to exit.");
    Console.ReadLine();
    Environment.Exit(1);
    return;
}

// if (inputFiles.Length == 1 && Directory.Exists(inputFiles[0]))
// {
//     inputFiles = Directory.GetFiles(inputFiles[0]);
// }

var totalFiles = inputFiles.Length;

foreach (var (inputFile, i) in inputFiles.Select((value, i) => (value, i)))
{
    Console.Title = $"[{i + 1}/{totalFiles}] {Path.GetFileName(inputFile)}";
    if (!Run(inputFile))
    {
        Console.WriteLine("Error! Press enter to exit.");
        Console.ReadLine();
        Environment.Exit(-1);
        return;
    }
}


bool Run(string inPath)
{
    var nameWithoutExtension = Path.GetFileNameWithoutExtension(inPath);
    var inParent = Path.GetDirectoryName(inPath)!;
    var extension = ".mkv";
    var outParent = outputPath!; // UseSelectedDir ? SelectedDir ?? inParent : inParent;
    var outPath = Path.Combine(outParent, nameWithoutExtension + "_merged" + extension);


    // first find files that we can merge for each input file
    var thingsToMerge =
        from fileName in Directory.GetFiles(inParent)
        let fileNameNoExt = Path.GetFileNameWithoutExtension(fileName)
        where fileName != inPath && fileNameNoExt.StartsWith(nameWithoutExtension)
        let extra = fileNameNoExt[nameWithoutExtension.Length..]
        let language = extra.Contains('.') ? extra.Split('.')[^1] : ""
        select new Tuple<string, string>(fileName, language);

    var inputArguments = new StringBuilder(inPath.Quote());

    foreach (var (fileName, language) in thingsToMerge)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            string lang;
            var lower = language.ToLower();
            if (lower.Contains("chs"))
            {
                lang = "zh";
            }
            else if (lower.Contains("cht"))
            {
                lang = "zh";
            }
            else if (lower.Contains("sc"))
            {
                lang = "zh";
            }
            else if (lower.Contains("tc"))
            {
                lang = "zh";
            }
            else if (lower.Length > 4)
            {
                lang = "und";
            }
            else
            {
                lang = language;
            }
            
            // mark language for all the tracks
            inputArguments.Append($" --language -1:{lang}");
        }

        inputArguments.Append(' ').Append(fileName.Quote());
    }

    cToken.ThrowIfCancellationRequested();

    try
    {
        var result1 = Utils.StartProcess("mkvmerge.exe", $"-o {outPath.Quote()} {inputArguments}", cToken);
        cToken.ThrowIfCancellationRequested();
        
        switch (result1)
        {
            case 0:
            case 1:
                break;
            default:
                Console.Error.WriteLine($"Error! mkvmerge can't mux the file! ({result1})");
                return false;
        }

        string fonts = "";
        string fontsFolder;
        if (Directory.Exists(Path.Combine(inParent, "fonts")))
        {
            fontsFolder = Path.Combine(inParent, "fonts");
        }
        else if (Directory.Exists(Path.Combine(inParent, "Fonts")))
        {
            fontsFolder = Path.Combine(inParent, "Fonts");
        }
        else
        {
            fontsFolder = "";
        }

        if (!string.IsNullOrWhiteSpace(fontsFolder))
        {
            fonts = Directory
                .GetFiles(fontsFolder)
                .Aggregate(new StringBuilder(" "), (s, fileName) => s.Append($" --add-attachment {fileName.Quote()}"))
                .ToString();
        }

        var result2 = Utils.StartProcess("mkvpropedit.exe", $"{outPath.Quote()}{fonts}", cToken);
        cToken.ThrowIfCancellationRequested();
        if (result2 != 0)
        {
            Console.Error.WriteLine($"Error! mkvpropedit returned non-zero exit code! ({result2})");
            return false;
        }

        var inFile = new FileInfo(inPath);
        var outFile = new FileInfo(outPath);


        if (CopyModifiedTime)
        {
            outFile.LastWriteTime = inFile.LastWriteTime;
        }

        // if (Configuration.DeleteOriginal)
        // {
        //     inFile.Delete();
        //     Console.WriteLine($"old file deleted: {inPath}");
        // }

        if (CopyFileName)
        {
            var newName = Path.Combine(outParent, nameWithoutExtension + extension);
            if (newName != inPath)
            {
                Console.WriteLine("Rename to old file name");
                outFile.MoveTo(newName, true);
            }
        }
        cToken.ThrowIfCancellationRequested();
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Operation cancelled.");
        return false;
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        return false;
    }

    return true;
}