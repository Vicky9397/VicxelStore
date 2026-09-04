namespace Dpm.Files.Domain;

public enum UploadStatus : byte
{
    InProgress = 1,
    Completed = 2,
    Aborted = 3,
}
