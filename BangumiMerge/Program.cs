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
    Console.WriteLine("Using output folder: " + outputPath);
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
var CopyModifiedTime = false;
var CopyFileName = false;


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

    // args for all the files to merge, sorted by language
    var filesArgs = thingsToMerge.Select(tuple =>
    {
        var (fileName, language) = tuple;
        IEnumerable<string> fileArg;
        if (!string.IsNullOrWhiteSpace(language))
        {
            if (language.Contains("chs", StringComparison.InvariantCultureIgnoreCase) ||
                language.Contains("sc", StringComparison.InvariantCultureIgnoreCase) ||
                language.Contains("gb", StringComparison.InvariantCultureIgnoreCase))
            {
                language = "zh-Hans";
                fileArg = ["--language", "-1:zh-Hans", "--track-name", "-1:简体中文"];
            }
            else if (language.Contains("cht", StringComparison.InvariantCultureIgnoreCase) ||
                     language.Contains("tc", StringComparison.InvariantCultureIgnoreCase) ||
                     language.Contains("big5", StringComparison.InvariantCultureIgnoreCase))
            {
                language = "zh-Hant";
                fileArg = ["--language", "-1:zh-Hant", "--track-name", "-1:繁體中文"];
            }
            else if (language.Equals("jp", StringComparison.InvariantCultureIgnoreCase) ||
                     language.Equals("jap", StringComparison.InvariantCultureIgnoreCase) ||
                     language.Equals("jpn", StringComparison.InvariantCultureIgnoreCase))
            {
                language = "jpn";
                fileArg = ["--language", "-1:jpn"];
            }
            else if (language.Length <= 3)
            {
                fileArg = ["--language", $"-1:{language}"];
            }
            else if (!Path.GetExtension(fileName).Equals(".mkv") &&
                     !Path.GetExtension(fileName).Equals(".mka") &&
                     !Path.GetExtension(fileName).Equals(".mp4") &&
                     !Path.GetExtension(fileName).Equals(".mov"))
            {
                fileArg = ["--track-name", $"-1:{language}"];
                language = "";
            }
            else
            {
                fileArg = [];
                language = "";
            }
        }
        else
        {
            fileArg = [];
            language = "";
        }

        fileArg = fileArg.Concat(["--default-track-flag", "-1:false"]).Append(fileName);
        return new KeyValuePair<string, IEnumerable<string>>(language, fileArg);
    }).OrderBy(pair => pair.Key).SelectMany(pair => pair.Value);

    IEnumerable<string> mergeArgs = ["-o", outPath, inPath];
    mergeArgs = mergeArgs.Concat(filesArgs);

    cToken.ThrowIfCancellationRequested();

    try
    {
        var result1 = Utils.StartProcess("mkvmerge", mergeArgs, cToken);
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
            Console.WriteLine("Fonts folder not found.");
            fontsFolder = "";
        }

        if (!string.IsNullOrWhiteSpace(fontsFolder))
        {
            var fonts = Directory.GetFiles(fontsFolder);

            if (fonts.Length == 0)
            {
                Console.WriteLine("Fonts folder is empty.");
            }
            else
            {
                var editArgs = fonts.SelectMany<string, string>(fileName => ["--add-attachment", fileName])
                    .Prepend(outPath);
                var result2 = Utils.StartProcess("mkvpropedit", editArgs, cToken);
                cToken.ThrowIfCancellationRequested();
                if (result2 != 0)
                {
                    Console.Error.WriteLine($"Error! mkvpropedit returned non-zero exit code! ({result2})");
                    return false;
                }
            }
            
        }
        
        var inFile = new FileInfo(inPath);
        var outFile = new FileInfo(outPath);


        if (CopyModifiedTime)
        {
            outFile.LastWriteTime = inFile.LastWriteTime;
        }

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