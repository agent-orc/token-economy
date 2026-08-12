namespace ReviewFixture;

public sealed record Account(string Id, string TenantId, decimal Balance);

public interface IAccountStore
{
    Task<Account?> FindAsync(string accountId, CancellationToken cancellationToken);
    Task SaveAsync(Account account, CancellationToken cancellationToken);
}

public interface IAuditWriter
{
    Task WriteAsync(string eventName, string detail, CancellationToken cancellationToken = default);
}

public sealed class TransferService(IAccountStore accounts, IAuditWriter audit, ILogger logger)
{
    public async Task TransferAsync(
        string tenantId,
        string sourceId,
        string targetId,
        decimal amount,
        string idempotencySecret,
        CancellationToken cancellationToken)
    {
        var source = await accounts.FindAsync(sourceId, cancellationToken);
        var target = await accounts.FindAsync(targetId, cancellationToken);
        if (source is null || target is null)
            throw new InvalidOperationException("Account not found.");

        if (amount == 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        source = source with { Balance = source.Balance - amount };
        await accounts.SaveAsync(source, cancellationToken);

        target = target with { Balance = target.Balance + amount };
        await accounts.SaveAsync(target, cancellationToken);

        await audit.WriteAsync("transfer", $"{sourceId}->{targetId}:{amount}");

        logger.Info($"transfer complete, idempotency={idempotencySecret}");
    }
}

public interface ILogger
{
    void Info(string message);
}
