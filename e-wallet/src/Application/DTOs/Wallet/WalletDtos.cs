namespace e_wallet.Application.DTOs.Wallet;

public class DepositRequest
{
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class WithdrawRequest
{
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class TransferRequest
{
    public Guid ToUserId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class WalletResponse
{
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class TransactionResponse
{
    public Guid Id { get; set; }
    public Guid? FromUserId { get; set; }
    public Guid? ToUserId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransactionListResponse
{
    public List<TransactionResponse> Transactions { get; set; } = new();
    public int Total { get; set; }
}
