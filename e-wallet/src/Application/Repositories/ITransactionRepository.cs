namespace e_wallet.Application.Repositories;

using e_wallet.Domain.Entities;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey);
    Task<Transaction> CreateAsync(Transaction transaction);
    Task<Transaction> UpdateAsync(Transaction transaction);
    Task<List<Transaction>> GetUserTransactionsAsync(Guid userId, int skip = 0, int take = 50);
    Task<int> GetUserTransactionsCountAsync(Guid userId);
}
