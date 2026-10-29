using NhaTro.Application.Abstractions;
using NhaTro.Application.UseCases;

namespace NhaTro.Api;

/// <summary>Releases rooms whose deposit hold has passed its hold-until date, checked hourly.</summary>
public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReleaseExpiredReservationsCommand>>();
                await handler.Handle(new ReleaseExpiredReservationsCommand(), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Releasing expired reservations failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}