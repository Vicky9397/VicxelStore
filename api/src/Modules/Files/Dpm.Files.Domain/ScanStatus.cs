namespace Dpm.Files.Domain;

public enum ScanStatus : byte
{
    Pending = 0,
    Clean = 1,
    Infected = 2,
}
