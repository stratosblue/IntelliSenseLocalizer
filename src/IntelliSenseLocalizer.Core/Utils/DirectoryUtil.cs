namespace System.IO;

public static class DirectoryUtil
{
    public static void CheckDirectory(string? directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
