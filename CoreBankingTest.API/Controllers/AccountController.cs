using AutoMapper;
using CoreBankingTest.API.Models;
using CoreBankingTest.API.Models.Requests;
using CoreBankingTest.APP.Accounts.Commands.CreateAccount;
using CoreBankingTest.APP.Accounts.Commands.TransferMoney;
using CoreBankingTest.APP.Accounts.Queries.GetAccountDetails;
using CoreBankingTest.APP.Accounts.Queries.GetTransactionHistory;
using CoreBankingTest.CORE.Interfaces;
using CoreBankingTest.CORE.Models;
using CoreBankingTest.CORE.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Security.Principal;

namespace CoreBankingTest.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AccountsController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;
        private readonly ILogger<AccountsController> _logger;

        public AccountsController(IMediator mediator, IMapper mapper, ILogger<AccountsController> logger)
        {
            _mediator = mediator;
            _mapper = mapper;
            _logger = logger;
        }

        /// <summary>
        /// Get account details by account number
        /// </summary>
        /// <param name="accountNumber">The 10-digit account number</param>
        /// <returns>Account details including balance and customer information</returns>
        /// <response code="200">Returns the account details</response>
        /// <response code="404">Account not found</response>
        /// <response code="400">Invalid account number format</response>
        [HttpGet("{accountNumber}")]
        [ProducesResponseType(typeof(ApiResponse<AccountDetailsDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<AccountDetailsDto>>> GetAccountDetails(string accountNumber)
        {
            _logger.LogInformation("Retrieving account details for {AccountNumber}", accountNumber);

            var query = new GetAccountDetailsQuery { AccountNumber = AccountNumber.Create(accountNumber) };
            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
                return NotFound(ApiResponse.CreateFailure(result.Errors));

            return Ok(ApiResponse<AccountDetailsDto>.CreateSuccess(result.Data!));
        }


        /// <summary>
        /// Create a new bank account
        /// </summary>
        /// <param name="request">Account creation details</param>
        /// <returns>The newly created account ID</returns>
        /// <response code="201">Account created successfully</response>
        /// <response code="400">Invalid request data or business rule violation</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<Guid>>> CreateAccount([FromBody] CreateAccountRequest request)
        {
            _logger.LogInformation("Creating new account for customer {CustomerId}", CustomerId.Create(request.CustomerId));

            var command = _mapper.Map<CreateAccountCommand>(request);
            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
                return BadRequest(ApiResponse.CreateFailure(result.Errors));

            return CreatedAtAction(
                nameof(GetAccountDetails),
                new { accountNumber = "TEMPORARY" }, // Would need account number here
                ApiResponse<Guid>.CreateSuccess(result.Data!));
        }


        /// <summary>
        /// Transfer money between accounts
        /// </summary>
        /// <param name="sourceaccountNumber">Source account number</param>
        /// <param name="destinationaccountNumber">Source account number</param>
        /// <param name="request">Transfer details</param>
        /// <returns>Transfer operation result</returns>
        /// <response code="200">Transfer completed successfully</response>
        /// <response code="400">Invalid transfer request</response>
        /// <response code="409">Business rule violation (e.g., insufficient funds)</response>
        [HttpPost("{accountNumber}/transfer")]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ApiResponse>> TransferMoney(
            string sourceaccountNumber,
            [FromBody] TransferMoneyRequest request)
        {
            _logger.LogInformation("Processing transfer from {AccountNumber}", sourceaccountNumber);

            var command = _mapper.Map<TransferMoneyCommand>(request);
            command = command with { SourceAccountNumber = AccountNumber.Create(sourceaccountNumber) };

            var result = await _mediator.Send(command);

            if (!result.IsSuccess)
            {
                return result.Errors.Any(e => e.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
                    ? Conflict(ApiResponse.CreateFailure(result.Errors))
                    : BadRequest(ApiResponse.CreateFailure(result.Errors));
            }

            return Ok(ApiResponse.CreateSuccess("Transfer completed successfully"));
        }

        [HttpGet("{accountNumber}/transactions")]
        [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<TransactionHistoryDto>>> GetTransactionHistory(
            string accountNumber,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
           
            var query = new GetTransactionHistoryQuery
            {
                AccountNumber = AccountNumber.Create(accountNumber),
                StartDate = startDate,
                EndDate = endDate,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);

            if (!result.IsSuccess)
                return NotFound(ApiResponse.CreateFailure(result.Errors));

            return Ok(ApiResponse<TransactionHistoryDto>.CreateSuccess(result.Data!));
        }
    }
}
