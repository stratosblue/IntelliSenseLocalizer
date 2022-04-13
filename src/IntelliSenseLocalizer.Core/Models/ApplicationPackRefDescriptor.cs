namespace IntelliSenseLocalizer.Models;

public class ApplicationPackRefDescriptor
{
    public string FrameworkMoniker { get; }

    public HashSet<IntelliSenseFileDescriptor> IntelliSenseFiles { get; } = new();

    public string PackName { get; }

    public Version PackVersion { get; }

    public ApplicationPackRefDescriptor(string packName, Version packVersion, string frameworkMoniker)
    {
        FrameworkMoniker = frameworkMoniker;
        PackName = packName;
        PackVersion = packVersion;
    }

    public override string ToString()
    {
        return $"[{PackName}]:[{PackVersion}] - [{FrameworkMoniker}] IntelliSenseFiles: {IntelliSenseFiles.Count}";
    }
}