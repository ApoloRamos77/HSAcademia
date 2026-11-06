using Microsoft.EntityFrameworkCore;

namespace HSAcademia.Infrastructure.Persistence;

public static class DbContextExtensions
{
    /// <summary>
    /// Ejecuta la operación dentro de una transacción compatible con EnableRetryOnFailure.
    /// Si ya hay una transacción abierta (operación anidada), se reutiliza.
    /// </summary>
    public static async Task<T> InTransactionAsync<T>(this AppDbContext db, Func<Task<T>> operation)
    {
        if (db.Database.CurrentTransaction != null)
            return await operation();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var result = await operation();
            await tx.CommitAsync();
            return result;
        });
    }
}
