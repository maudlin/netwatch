namespace NetworkHUD.Models
{
    public record PingSample(long TimestampMs, bool Success, int RttMs);
    public record DnsSample(long TimestampMs, int Ms);
}
