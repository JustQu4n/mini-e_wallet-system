namespace e_wallet.Application.Services;

using Microsoft.EntityFrameworkCore;
using Serilog;
using e_wallet.Application.DTOs.Wallet;
using e_wallet.Application.Exceptions;
using e_wallet.Application.Repositories;
using e_wallet.Domain.Entities;
using e_wallet.Infrastructure.Data;

public class WalletService : IWalletService
{
    private readonly IUserRepository _userRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly WalletDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly ILogger _logger;

    public WalletService(
        IUserRepository userRepository,
        ITransactionRepository transactionRepository,
        WalletDbContext dbContext,
        ICacheService cacheService,
        ILogger logger)
    {
        _userRepository = userRepository;
        _transactionRepository = transactionRepository;
        _dbContext = dbContext;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<WalletResponse> GetBalanceAsync(Guid userId)
    {
        // Try to get from cache first
        var cacheKey = $"balance_{userId}";
        var cachedBalance = await _cacheService.GetAsync<WalletResponse>(cacheKey);
        if (cachedBalance != null)
        {
            _logger.Debug("Balance retrieved from cache for user: {UserId}", userId);
            return cachedBalance;
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        var response = new WalletResponse
        {
            UserId = user.Id,
            Balance = user.Balance,
            LastUpdated = user.UpdatedAt
        };

        // Cache for 5 minutes
        await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

        return response;
    }

    public async Task<TransactionResponse> DepositAsync(Guid userId, DepositRequest request)
    {
        try
        {
            // Check for duplicate request
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existingTxn = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existingTxn != null)
                {
                    _logger.Information("Duplicate deposit detected with idempotency key: {Key}", request.IdempotencyKey);
                    return MapTransactionToResponse(existingTxn);
                }
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new UserNotFoundException(userId);
            }

            // Start transaction for ACID compliance
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Lock the user row for update
                user = await _dbContext.Users
                    .FromSqlInterpolated($"SELECT * FROM Users WHERE Id = {userId} WITH (UPDLOCK, ROWLOCK)")
                    .FirstOrDefaultAsync() ?? throw new UserNotFoundException(userId);

                // Update balance
                user.Balance += request.Amount;
                user.Version++;

                // Create transaction record
                var walletTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    FromUserId = null,
                    ToUserId = userId,
                    Amount = request.Amount,
                    Type = TransactionType.Deposit,
                    Status = TransactionStatus.Success,
                    IdempotencyKey = request.IdempotencyKey,
                    Description = request.Description
                };

                await _userRepository.UpdateAsync(user);
                await _transactionRepository.CreateAsync(walletTransaction);

                await transaction.CommitAsync();

                _logger.Information("Deposit successful: {UserId}, Amount: {Amount}", userId, request.Amount);

                // Invalidate cache
                await _cacheService.RemoveAsync($"balance_{userId}");

                // Schedule background job for notification (would use Hangfire)
                // BackgroundJob.Enqueue(() => _notificationService.SendDepositNotificationAsync(userId, request.Amount));

                return MapTransactionToResponse(walletTransaction);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Error during deposit for user: {UserId}", userId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Deposit operation failed");
            throw;
        }
    }

    public async Task<TransactionResponse> WithdrawAsync(Guid userId, WithdrawRequest request)
    {
        try
        {
            // Check for duplicate request
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existingTxn = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existingTxn != null)
                {
                    _logger.Information("Duplicate withdrawal detected with idempotency key: {Key}", request.IdempotencyKey);
                    return MapTransactionToResponse(existingTxn);
                }
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new UserNotFoundException(userId);
            }

            // Verify sufficient funds
            if (user.Balance < request.Amount)
            {
                _logger.Warning("Insufficient funds for withdrawal: {UserId}, Balance: {Balance}, Amount: {Amount}",
                    userId, user.Balance, request.Amount);
                throw new InsufficientFundsException();
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Lock the user row for update
                user = await _dbContext.Users
                    .FromSqlInterpolated($"SELECT * FROM Users WHERE Id = {userId} WITH (UPDLOCK, ROWLOCK)")
                    .FirstOrDefaultAsync() ?? throw new UserNotFoundException(userId);

                // Double-check balance after lock
                if (user.Balance < request.Amount)
                {
                    throw new InsufficientFundsException();
                }

                // Update balance
                user.Balance -= request.Amount;
                user.Version++;

                // Create transaction record
                var walletTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    FromUserId = userId,
                    ToUserId = null,
                    Amount = request.Amount,
                    Type = TransactionType.Withdraw,
                    Status = TransactionStatus.Success,
                    IdempotencyKey = request.IdempotencyKey,
                    Description = request.Description
                };

                await _userRepository.UpdateAsync(user);
                await _transactionRepository.CreateAsync(walletTransaction);

                await transaction.CommitAsync();

                _logger.Information("Withdrawal successful: {UserId}, Amount: {Amount}", userId, request.Amount);

                // Invalidate cache
                await _cacheService.RemoveAsync($"balance_{userId}");

                return MapTransactionToResponse(walletTransaction);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Error during withdrawal for user: {UserId}", userId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Withdrawal operation failed");
            throw;
        }
    }

    public async Task<TransactionResponse> TransferAsync(Guid fromUserId, TransferRequest request)
    {
        try
        {
            // Validate recipient
            if (request.ToUserId == fromUserId)
            {
                throw new InvalidOperationException("Cannot transfer to the same account.");
            }

            // Check for duplicate request
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existingTxn = await _transactionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey);
                if (existingTxn != null)
                {
                    _logger.Information("Duplicate transfer detected with idempotency key: {Key}", request.IdempotencyKey);
                    return MapTransactionToResponse(existingTxn);
                }
            }

            var fromUser = await _userRepository.GetByIdAsync(fromUserId);
            if (fromUser == null)
            {
                throw new UserNotFoundException(fromUserId);
            }

            var toUser = await _userRepository.GetByIdAsync(request.ToUserId);
            if (toUser == null)
            {
                throw new UserNotFoundException(request.ToUserId);
            }

            // Verify sufficient funds
            if (fromUser.Balance < request.Amount)
            {
                throw new InsufficientFundsException();
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // Lock both user rows (lock sender first to prevent deadlock)
                var lockQuery = await _dbContext.Users
                    .FromSqlInterpolated($"SELECT * FROM Users WHERE Id IN ({fromUserId}, {request.ToUserId}) ORDER BY Id WITH (UPDLOCK, ROWLOCK)")
                    .ToListAsync();

                fromUser = lockQuery.FirstOrDefault(u => u.Id == fromUserId) ?? throw new UserNotFoundException(fromUserId);
                toUser = lockQuery.FirstOrDefault(u => u.Id == request.ToUserId) ?? throw new UserNotFoundException(request.ToUserId);

                // Double-check balance after lock
                if (fromUser.Balance < request.Amount)
                {
                    throw new InsufficientFundsException();
                }

                // Update balances
                fromUser.Balance -= request.Amount;
                fromUser.Version++;
                toUser.Balance += request.Amount;
                toUser.Version++;

                // Create transaction record
                var walletTransaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    FromUserId = fromUserId,
                    ToUserId = request.ToUserId,
                    Amount = request.Amount,
                    Type = TransactionType.Transfer,
                    Status = TransactionStatus.Success,
                    IdempotencyKey = request.IdempotencyKey,
                    Description = request.Description
                };

                await _userRepository.UpdateAsync(fromUser);
                await _userRepository.UpdateAsync(toUser);
                await _transactionRepository.CreateAsync(walletTransaction);

                await transaction.CommitAsync();

                _logger.Information("Transfer successful: From {FromUserId} to {ToUserId}, Amount: {Amount}",
                    fromUserId, request.ToUserId, request.Amount);

                // Invalidate cache for both users
                await _cacheService.RemoveAsync($"balance_{fromUserId}");
                await _cacheService.RemoveAsync($"balance_{request.ToUserId}");

                return MapTransactionToResponse(walletTransaction);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Concurrency error during transfer");
                throw new ConcurrencyException("Transfer failed due to concurrent modification. Please try again.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.Error(ex, "Error during transfer from {FromUserId} to {ToUserId}", fromUserId, request.ToUserId);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Transfer operation failed");
            throw;
        }
    }

    public async Task<TransactionListResponse> GetTransactionsAsync(Guid userId, int page = 1, int pageSize = 50)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new UserNotFoundException(userId);
        }

        var skip = (page - 1) * pageSize;
        var transactions = await _transactionRepository.GetUserTransactionsAsync(userId, skip, pageSize);
        var total = await _transactionRepository.GetUserTransactionsCountAsync(userId);

        var response = new TransactionListResponse
        {
            Transactions = transactions.Select(MapTransactionToResponse).ToList(),
            Total = total
        };

        _logger.Debug("Retrieved {Count} transactions for user {UserId}", transactions.Count, userId);

        return response;
    }

    private TransactionResponse MapTransactionToResponse(Transaction transaction)
    {
        return new TransactionResponse
        {
            Id = transaction.Id,
            FromUserId = transaction.FromUserId,
            ToUserId = transaction.ToUserId,
            Amount = transaction.Amount,
            Type = transaction.Type.ToString(),
            Status = transaction.Status.ToString(),
            Description = transaction.Description,
            CreatedAt = transaction.CreatedAt
        };
    }
}
