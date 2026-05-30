namespace CompraPassagemOnline.Application.Common;

public static class ReservationOptions
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SearchCacheTtl = TimeSpan.FromSeconds(60);
}
