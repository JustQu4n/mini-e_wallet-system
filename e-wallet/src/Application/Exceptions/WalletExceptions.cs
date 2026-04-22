namespace e_wallet.Application.Exceptions;

public class WalletException : Exception
{
    public WalletException(string message) : base(message)
    {
    }
}

public class InsufficientFundsException : WalletException
{
    public InsufficientFundsException() : base("Insufficient funds for this operation.")
    {
    }
}

public class UserNotFoundException : WalletException
{
    public UserNotFoundException(Guid userId) : base($"User with ID {userId} not found.")
    {
    }

    public UserNotFoundException(string email) : base($"User with email {email} not found.")
    {
    }
}

public class InvalidOperationException : WalletException
{
    public InvalidOperationException(string message) : base(message)
    {
    }
}

public class ConcurrencyException : WalletException
{
    public ConcurrencyException(string message) : base(message)
    {
    }
}

public class DuplicateEmailException : WalletException
{
    public DuplicateEmailException(string email) : base($"User with email {email} already exists.")
    {
    }
}

public class InvalidCredentialsException : WalletException
{
    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }
}
