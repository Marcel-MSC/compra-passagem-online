using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompraPassagemOnline.Application.Booking;
using CompraPassagemOnline.Application.Inventory;
using CompraPassagemOnline.Application.Payments;
using CompraPassagemOnline.Domain.Enums;

namespace CompraPassagemOnline.Tests.Infrastructure;

public sealed class PurchaseFlowClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;

    public PurchaseFlowClient(HttpClient client) => _client = client;

    public async Task<IReadOnlyList<SeatDto>> GetAvailableSeatsAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        var seats = await _client.GetFromJsonAsync<List<SeatDto>>(
            $"/api/trips/{tripId}/seats",
            JsonOptions,
            cancellationToken);

        return seats?
            .Where(s => s.Status == SeatStatus.Available)
            .ToList() ?? [];
    }

    public async Task<HttpResponseMessage> ReserveSeatAsync(
        Guid tripId,
        Guid seatId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _client.PostAsJsonAsync(
            "/api/reservations",
            new { tripId, seatId, userId },
            cancellationToken);
    }

    public async Task<ReservationDto?> ReserveSeatOrNullAsync(
        Guid tripId,
        Guid seatId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var response = await ReserveSeatAsync(tripId, seatId, userId, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<ReservationDto>(JsonOptions, cancellationToken);
    }

    public async Task<OrderDto> CreateOrderAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                reservationId,
                passengers = new[] { new { fullName = "Test User", documentNumber = "12345678900" } }
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions, cancellationToken))!;
    }

    public async Task<HttpResponseMessage> PayAsync(
        Guid orderId,
        string idempotencyKey,
        bool simulateFailure = false,
        CancellationToken cancellationToken = default)
    {
        return await _client.PostAsJsonAsync(
            "/api/payments",
            new { orderId, idempotencyKey, simulateFailure },
            cancellationToken);
    }

    public async Task<PaymentResultDto?> PayOrNullAsync(
        Guid orderId,
        string idempotencyKey,
        bool simulateFailure = false,
        CancellationToken cancellationToken = default)
    {
        var response = await PayAsync(orderId, idempotencyKey, simulateFailure, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<PaymentResultDto>(JsonOptions, cancellationToken);
    }

    public async Task<SeatDto?> GetSeatAsync(Guid tripId, Guid seatId, CancellationToken cancellationToken = default)
    {
        var seats = await _client.GetFromJsonAsync<List<SeatDto>>(
            $"/api/trips/{tripId}/seats",
            JsonOptions,
            cancellationToken);

        return seats?.FirstOrDefault(s => s.Id == seatId);
    }
}
