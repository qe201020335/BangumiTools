using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BangumiMerge;

Console.OutputEncoding = Encoding.UTF8;

var outputPath = @"T:/Video";
var inputFiles = args;
bool CopyModifiedTime = true;
bool CopyFileName = true;

if (inputFiles.Length == 0)
{
    Console.WriteLine("Drag and drop files to this exe to merge them.");
    Console.WriteLine("Press any key to exit.");
    Console.ReadKey();
    return;
}

foreach (var inputFile in inputFiles)
{
    if (!Run(inputFile))
    {
        Console.WriteLine("Error! Press any key to exit.");
        Console.ReadKey();
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
            // mark language for all the tracks
            inputArguments.Append($" --language -1:{language}");
        }
        
        inputArguments.Append(' ').Append(fileName.Quote());
    }

    try
    {
        var result1 = Utils.StartProcess("mkvmerge.exe", $"-o {outPath.Quote()} {inputArguments}");
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
        } else if (Directory.Exists(Path.Combine(inParent, "Fonts")))
        {
            fontsFolder = Path.Combine(inParent, "Fonts");
        } else
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

        var result2 = Utils.StartProcess("mkvpropedit.exe", $"{outPath.Quote()}{fonts}");
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
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        return false;
    }

    return true;
}