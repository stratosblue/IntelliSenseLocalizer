namespace IntelliSenseLocalizer.Nuget;

public static class GlobalPackagesFinder
{
    public static readonly string Location;

    static GlobalPackagesFinder()
    {
        //see https://learn.microsoft.com/zh-cn/nuget/consume-packages/managing-the-global-packages-and-cache-folders
        Location = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    public static IEnumerable<NugetGlobalPackageDescriptor> EnumeratePackages()
    {
        if (!Directory.Exists(Location))
        {
            yield break;
        }
        foreach (var packageFolder in Directory.EnumerateDirectories(Location, "*", SearchOption.TopDirectoryOnly))
        {
            var normalizedName = Path.GetFileName(packageFolder);

            yield return new NugetGlobalPackageDescriptor(normalizedName, packageFolder);
        }
    }
}
