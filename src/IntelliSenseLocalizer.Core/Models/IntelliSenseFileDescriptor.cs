namespace IntelliSenseLocalizer.Models;

public record class IntelliSenseFileDescriptor(ApplicationPackRefDescriptor OwnerPackRef, string Name, string FileName, string FilePath);
