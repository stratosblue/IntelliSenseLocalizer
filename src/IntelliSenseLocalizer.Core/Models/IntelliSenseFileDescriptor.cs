using System.Globalization;

namespace IntelliSenseLocalizer.Models;

public record class IntelliSenseFileDescriptor(string Name, string FileName, string FilePath, string PackName, string Moniker, CultureInfo? Culture);
