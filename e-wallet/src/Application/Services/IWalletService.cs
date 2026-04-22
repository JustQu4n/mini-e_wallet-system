namespace e_wallet.Application.Services;

using e_wallet.Application.DTOs.Wallet;

public interface IWalletService
{
    Task<WalletResponse> GetBalanceAsync(Guid userId);
    Task<TransactionResponse> DepositAsync(Guid userId, DepositRequest request);
    Task<TransactionResponse> WithdrawAsync(Guid userId, WithdrawRequest request);
    Task<TransactionResponse> TransferAsync(Guid fromUserId, TransferRequest request);
    Task<TransactionListResponse> GetTransactionsAsync(Guid userId, int page = 1, int pageSize = 50);
}
