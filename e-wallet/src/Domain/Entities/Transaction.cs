namespace e_wallet.Domain.Entities;

public enum TransactionType
{
    Deposit,
    Withdraw,
    Transfer
}

public enum TransactionStatus
{
    Pending,
    Success,
    Failed
}

public class Transaction
{
    public Guid Id { get; set; }
    public Guid? FromUserId { get; set; } // Null for deposits
    public Guid? ToUserId { get; set; } // Null for withdrawals
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User? FromUser { get; set; }
    public User? ToUser { get; set; }
}
