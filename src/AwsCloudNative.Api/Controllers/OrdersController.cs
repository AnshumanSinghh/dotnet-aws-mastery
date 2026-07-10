using AwsCloudNative.Common.Constants;
using AwsCloudNative.Data;
using AwsCloudNative.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Provides CRUD (Create Read Update Delete) operations for Orders
    /// backed by Amazon RDS PostgreSQL via EF Core 10.
    /// Controller is deliberately thin — EF Core is used directly here
    /// as there is no separate service layer for simple CRUD operations.
    /// For complex business logic, delegate to a service class.
    /// </summary>
    [ApiController]
    [Route("api/orders")]
    [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
    public sealed class OrdersController : ControllerBase
    {
        private readonly OrdersDbContext _dbContext;
        private readonly ILogger<OrdersController> _logger;

        /// <summary>
        /// DbContext is Scoped — one instance per HTTP request.
        /// All operations within one request share the same transaction boundary.
        /// </summary>
        public OrdersController(
            OrdersDbContext dbContext,
            ILogger<OrdersController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves all orders belonging to the authenticated customer.
        /// WHY filter by CustomerId from JWT:
        /// Never trust a CustomerId from the request body or query string.
        /// Always derive identity from the validated JWT sub claim —
        /// otherwise any user can query any other user's orders.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetOrders(CancellationToken ct)
        {
            var customerId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject)!;

            var orders = await _dbContext.Orders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAtUtc)
                .Select(o => new
                {
                    o.OrderId,
                    o.Amount,
                    o.Status,
                    o.CreatedAtUtc
                })
                .AsNoTracking() // WHY AsNoTracking: read-only query — no change tracking needed
                .ToListAsync(ct);

            return Ok(orders);
        }

        /// <summary>
        /// Retrieves a single order by its business OrderId.
        /// Validates ownership — only the owning customer can retrieve their order.
        /// </summary>
        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrder(string orderId, CancellationToken ct)
        {
            var customerId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject)!;

            var order = await _dbContext.Orders
                .Where(o => o.OrderId == orderId && o.CustomerId == customerId)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);

            if (order is null)
                return NotFound(new { Error = $"Order '{orderId}' not found." });

            return Ok(order);
        }

        /// <summary>
        /// Creates a new order record in RDS.
        /// Uses application-generated UUID as the database PK.
        /// Returns 201 Created with the Location header pointing to the new resource.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderRequest request,
            CancellationToken ct)
        {
            var customerId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject)!;

            var order = new OrderEntity
            {
                Id = Guid.NewGuid(),
                OrderId = request.OrderId,
                CustomerId = customerId,
                Amount = request.Amount,
                Status = OrderStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Orders.Add(order);

            try
            {
                await _dbContext.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
                when (ex.InnerException?.Message.Contains("ix_orders_order_id") == true)
            {
                // Unique index violation on OrderId — duplicate order submission.
                // Return 409 Conflict, not 500 — this is a business rule violation.
                _logger.LogWarning(
                    "Duplicate OrderId submitted. OrderId={OrderId}", request.OrderId);

                return Conflict(new { Error = $"Order '{request.OrderId}' already exists." });
            }

            _logger.LogInformation(
                "Order created. OrderId={OrderId} CustomerId={CustomerId}",
                order.OrderId, customerId);

            return CreatedAtAction(
                nameof(GetOrder),
                new { orderId = order.OrderId },
                new { order.OrderId, order.Status, order.CreatedAtUtc });
        }

        /// <summary>
        /// Updates an order's status using EF Core's ExecuteUpdateAsync.
        /// WHY ExecuteUpdateAsync over loading the entity then saving:
        /// ExecuteUpdateAsync generates a single UPDATE SQL statement without
        /// loading the entity into memory first. For status updates where you
        /// do not need the full entity, this is significantly more efficient.
        /// PITFALL: ExecuteUpdateAsync bypasses EF Core change tracking and
        /// concurrency tokens — only use for simple targeted updates.
        /// </summary>
        [HttpPatch("{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(
            string orderId,
            [FromBody] UpdateStatusRequest request,
            CancellationToken ct)
        {
            var customerId = User.FindFirstValue(AuthConstants.CognitoClaims.Subject)!;

            var rowsAffected = await _dbContext.Orders
                .Where(o => o.OrderId == orderId && o.CustomerId == customerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.Status, request.Status)
                    .SetProperty(o => o.UpdatedAtUtc, DateTime.UtcNow),
                ct);

            if (rowsAffected == 0)
                return NotFound(new { Error = $"Order '{orderId}' not found." });

            return NoContent();
        }
    }

    /// <summary>Request model for creating a new order.</summary>
    public sealed record CreateOrderRequest
    {
        /// <summary>Business-level order identifier — must be unique per customer.</summary>
        public string OrderId { get; init; } = string.Empty;

        /// <summary>Order amount — must be positive.</summary>
        public decimal Amount { get; init; }
    }

    /// <summary>Request model for updating an order's status.</summary>
    public sealed record UpdateStatusRequest
    {
        /// <summary>
        /// New order status. Must be one of the values defined in OrderStatus constants.
        /// </summary>
        public string Status { get; init; } = string.Empty;
    }
}
