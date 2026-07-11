using AwsCloudNative.Api.Models;
using AwsCloudNative.Api.Services;
using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Provides product catalog CRUD operations backed by Amazon DynamoDB.
    /// Thin controller — all DynamoDB access patterns live in ProductService.
    /// </summary>
    [ApiController]
    [Route("api/products")]
    [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
    public sealed class ProductController : ControllerBase
    {
        private readonly ProductService _productService;
        private readonly ILogger<ProductController> _logger;

        /// <summary>Initialises with ProductService and logger.</summary>
        public ProductController(
            ProductService productService,
            ILogger<ProductController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a single product by its business ProductId.
        /// Uses GetItem — O(1) DynamoDB lookup by PK + SK.
        /// </summary>
        [HttpGet("{productId}")]
        public async Task<IActionResult> GetProduct(
            string productId,
            CancellationToken ct)
        {
            var product = await _productService.GetProductAsync(productId, ct);

            if (product is null)
                return NotFound(new { Error = $"Product '{productId}' not found." });

            return Ok(product);
        }

        /// <summary>
        /// Queries all products in a given category using the CategoryIndex GSI.
        /// Returns an empty array (not 404) when no products exist in the category.
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetProductsByCategory(
            string category,
            CancellationToken ct)
        {
            var products = await _productService
                .GetProductsByCategoryAsync(category, ct);

            return Ok(products);
        }

        /// <summary>
        /// Creates or replaces a product.
        /// Uses PutItem semantics — full replacement if item exists.
        /// Returns 201 Created with the product's location.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = AuthConstants.Policies.AdminOnly)]
        public async Task<IActionResult> CreateProduct(
            [FromBody] ProductItem product,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(product.ProductId))
                return BadRequest(new { Error = "ProductId is required." });

            await _productService.SaveProductAsync(product, ct);

            return CreatedAtAction(
                nameof(GetProduct),
                new { productId = product.ProductId },
                new { product.ProductId, product.Name, product.Category });
        }

        /// <summary>
        /// Deletes a product by ProductId.
        /// Idempotent — deleting a non-existent product returns 204.
        /// </summary>
        [HttpDelete("{productId}")]
        [Authorize(Policy = AuthConstants.Policies.AdminOnly)]
        public async Task<IActionResult> DeleteProduct(
            string productId,
            CancellationToken ct)
        {
            await _productService.DeleteProductAsync(productId, ct);
            return NoContent();
        }
    }
}
