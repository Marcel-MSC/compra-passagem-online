namespace CompraPassagemOnline.Application.Common;

public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan SearchCacheTtl { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan ExpiryWorkerInterval { get; set; } = TimeSpan.FromSeconds(30);
}
