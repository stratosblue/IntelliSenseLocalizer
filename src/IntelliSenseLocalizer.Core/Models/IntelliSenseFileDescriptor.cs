namespace IntelliSenseLocalizer.Models;

public record class IntelliSenseFileDescriptor(ApplicationPackRefDescriptor OwnerPack, string Name, string FileName, string FilePath);
