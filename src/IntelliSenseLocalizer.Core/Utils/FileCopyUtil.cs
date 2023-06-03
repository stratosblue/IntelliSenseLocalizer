namespace IntelliSenseLocalizer;

public static class FileCopyUtil
{
    public static IEnumerable<(string From, string To)> CopyDirectory(string source, string target, string fileSearchPattern, Func<string, bool> filter, bool overwrite, bool recursive)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"Source directory {source} not found.");
        }

        DirectoryUtil.CheckDirectory(target);

        foreach (var sourceFilePath in Directory.EnumerateFiles(source, fileSearchPattern, SearchOption.TopDirectoryOnly))
        {
            if (!filter(sourceFilePath))
            {
                continue;
            }
            var fileName = Path.GetFileName(sourceFilePath);

            var targetFilePath = Path.Combine(target, fileName);

            File.Copy(sourceFilePath, targetFilePath, overwrite);

            yield return (sourceFilePath, targetFilePath);
        }

        if (recursive)
        {
            foreach (var sourceSubDirectory in Directory.EnumerateDirectories(source))
            {
                var directoryName = Path.GetFileName(sourceSubDirectory);

                if (string.IsNullOrEmpty(directoryName))
                {
                    continue;
                }

                var targetSubDirectory = Path.Combine(target, directoryName);

                foreach (var item in CopyDirectory(sourceSubDirectory, targetSubDirectory, fileSearchPattern, filter, overwrite, recursive))
                {
                    yield return item;
                }
            }
        }
    }
}
