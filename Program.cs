using System.Globalization;

namespace PaperlessCopier;

static class Program
{
    private static DirectoryInfo? _srcDir;
    private static DirectoryInfo _destDir = new("/volume1/dockerdata/paperless/data/consume");
    private static DateTime? _earliestDate = DateTime.MinValue;

    private static string? _timestampPath;

    // Skip ".jpg", ".jpeg", ".webp", ".png", ".txt",  for now as we don't want them to need to be OCR'd
    // Also, Tika doesn't support  ".xlsx"
    private static readonly string[] DocumentExtensions = { ".pdf", ".docx", ".xlsx" };

    static void Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
                throw new ArgumentException("Please specify source folder.");

            _srcDir = new DirectoryInfo(args[0]);

            if (args.Length == 2)
                _destDir = new DirectoryInfo(args[1]);

            if (!_srcDir.Exists)
                throw new ArgumentException($"Source folder {_srcDir} does not exist.");

            if (!_destDir.Exists)
                throw new ArgumentException($"Destination folder {_destDir} does not exist.");

            _timestampPath = Path.Combine(_srcDir.FullName, ".PaperlessImportTimestamp");

            ReadLastImportDate();
            ScanForDcuments();

            Console.WriteLine("Trimming empty folders...");
            TrimEmptyDirs(_destDir);

            Console.WriteLine("Import process complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
            Thread.Sleep(6 * 1000);
        }
    }

    private static void TrimEmptyDirs(DirectoryInfo dir)
    {
        var subdirs = dir.GetDirectories();

        foreach (var subDir in subdirs)
            TrimEmptyDirs(subDir);

        var files = dir.GetFiles();

        var dsStore = files.FirstOrDefault(x => x.Name == ".DS_Store");

        if (dsStore != null)
        {

            dsStore.Delete();
            files = dir.GetFiles();
        }

        if (files.Any())
            return;

        if (dir.GetDirectories().Any())
            return;
        
        try
        {
            dir.Delete();
            Console.WriteLine($"Deleted folder {dir.FullName}.");
        }
        catch
        {
            Console.WriteLine($"Unable to delete folder {dir.FullName}.");
        }
    }

    private static void ReadLastImportDate()
    {
        if (File.Exists(_timestampPath))
        {
            var lines = File.ReadAllLines(_timestampPath);

            _earliestDate = DateTime.MinValue;

            if (DateTime.TryParseExact(lines.FirstOrDefault(), "dd-MMM-yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                _earliestDate = date;
            }
        }

        Console.WriteLine($"Importing docs with timestamp since {_earliestDate:dd-MMM-yyyy HH:mm:ss}");
    }

    private static void ScanForDcuments()
    {
        Console.WriteLine($"Starting photo import from {_srcDir!.FullName}...");

        var allFiles = _srcDir.GetFiles("*.*", SearchOption.AllDirectories);

        Console.WriteLine($"Found {allFiles.Length} files in source folder.");

        var filteredFiles = allFiles.Where(x => x.LastAccessTimeUtc > _earliestDate &&
                                                DocumentExtensions.Contains(x.Extension,
                                                    StringComparer.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"Found {filteredFiles.Count} files that match the filter.");

        int copied = 0;
        int skipped = 0;
        int errored = 0;

        foreach (var file in filteredFiles)
        {
            var relativePath = MakePathRelativeTo(file.FullName, _srcDir.FullName);

            var destFile = new FileInfo(Path.Combine(_destDir.FullName, relativePath));

            if (destFile.Exists)
            {
                Console.WriteLine($"{destFile} already existed. Skipping...");
                skipped++;
                continue;
            }

            Console.WriteLine($"Copying {file.FullName} to {_destDir.Name}...");

            try
            {
                if (!destFile.Directory.Exists)
                {
                    Console.WriteLine($"Creating directory {destFile.Directory}");
                    Directory.CreateDirectory(destFile.Directory.FullName);
                }

                File.Copy(file.FullName, destFile.FullName);
                copied++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error copying {destFile}: {ex.Message}");
                errored++;
            }
        }

        Console.WriteLine($"Copied {copied} files, skipped {skipped}, errors: {errored}.");
        Console.WriteLine("Writing timestamp...");
        File.WriteAllText(_timestampPath!, $"{DateTime.UtcNow:dd-MMM-yyyy HH:mm:ss}");
    }

    public static string MakePathRelativeTo(this string path, string root)
    {
        if (!root.EndsWith(Path.DirectorySeparatorChar))
            root += Path.DirectorySeparatorChar;

        var result = Path.GetRelativePath(root, path);
        return result;
    }
}