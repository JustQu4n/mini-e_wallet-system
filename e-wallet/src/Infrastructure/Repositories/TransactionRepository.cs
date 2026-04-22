namespace e_wallet.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using e_wallet.Application.Repositories;
using e_wallet.Domain.Entities;
using e_wallet.Infrastructure.Data;

public class TransactionRepository : ITransactionRepository
{
    private readonly WalletDbContext _context;

    public TransactionRepository(WalletDbContext context)
    {
        _context = context;
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        return await _context.Transactions
            .Include(t => t.FromUser)
            .Include(t => t.ToUser)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        transaction.CreatedAt = DateTime.UtcNow;
        transaction.UpdatedAt = DateTime.UtcNow;
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<Transaction> UpdateAsync(Transaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        _context.Transactions.Update(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(Guid userId, int skip = 0, int take = 50)
    {
        return await _context.Transactions
            .Where(t => t.FromUserId == userId || t.ToUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Include(t => t.FromUser)
            .Include(t => t.ToUser)
            .ToListAsync();
    }

    public async Task<int> GetUserTransactionsCountAsync(Guid userId)
    {
        return await _context.Transactions
            .Where(t => t.FromUserId == userId || t.ToUserId == userId)
            .CountAsync();
    }
}
