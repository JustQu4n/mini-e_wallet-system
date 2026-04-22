namespace e_wallet.API.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using e_wallet.Application.DTOs.Wallet;
using e_wallet.Application.DTOs.Common;
using e_wallet.Application.Services;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly ILogger<WalletController> _logger;

    public WalletController(IWalletService walletService, ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(userIdClaim ?? throw new InvalidOperationException("User ID not found in token"));
    }

    /// <summary>
    /// Get wallet balance
    /// </summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(ApiResponse<WalletResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance()
    {
        try
        {
            var userId = GetUserId();
            var balance = await _walletService.GetBalanceAsync(userId);
            return Ok(ApiResponse<WalletResponse>.SuccessResponse(balance));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving balance");
            return NotFound(ApiResponse.FailureResponse(ex.Message));
        }
    }

    /// <summary>
    /// Deposit money to wallet
    /// </summary>
    [HttpPost("deposit")]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
    {
        try
        {
            var userId = GetUserId();
            var transaction = await _walletService.DepositAsync(userId, request);
            return Ok(ApiResponse<TransactionResponse>.SuccessResponse(transaction, "Deposit successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deposit error");
            return BadRequest(ApiResponse.FailureResponse(ex.Message));
        }
    }

    /// <summary>
    /// Withdraw money from wallet
    /// </summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request)
    {
        try
        {
            var userId = GetUserId();
            var transaction = await _walletService.WithdrawAsync(userId, request);
            return Ok(ApiResponse<TransactionResponse>.SuccessResponse(transaction, "Withdrawal successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Withdrawal error");
            return BadRequest(ApiResponse.FailureResponse(ex.Message));
        }
    }

    /// <summary>
    /// Transfer money to another user
    /// </summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
    {
        try
        {
            var userId = GetUserId();
            var transaction = await _walletService.TransferAsync(userId, request);
            return Ok(ApiResponse<TransactionResponse>.SuccessResponse(transaction, "Transfer successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer error");
            return BadRequest(ApiResponse.FailureResponse(ex.Message));
        }
    }

    /// <summary>
    /// Get transaction history
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(ApiResponse<TransactionListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var userId = GetUserId();
            var transactions = await _walletService.GetTransactionsAsync(userId, page, pageSize);
            return Ok(ApiResponse<TransactionListResponse>.SuccessResponse(transactions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transactions");
            return NotFound(ApiResponse.FailureResponse(ex.Message));
        }
    }
}
