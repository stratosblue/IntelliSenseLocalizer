namespace IntelliSenseLocalizer.Models;

public class ApplicationPackDescriptor
{
    public Version DotnetVersion { get; }

    public string Name { get; }

    public HashSet<ApplicationPackRefDescriptor> PackRefs { get; } = new();

    public string RootPath { get; }

    public ApplicationPackDescriptor(string name, string rootPath, Version dotnetVersion)
    {
        Name = name;
        RootPath = rootPath;
        DotnetVersion = dotnetVersion;
    }

    public override string ToString()
    {
        return $"[{Name}]:[{DotnetVersion}] at [{RootPath}]";
    }
}