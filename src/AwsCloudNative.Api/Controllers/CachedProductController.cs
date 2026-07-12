using AwsCloudNative.Api.Models;
using AwsCloudNative.Api.Services;
using AwsCloudNative.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AwsCloudNative.Api.Controllers
{
    /// <summary>
    /// Demonstrates the cache-aside pattern with ElastiCache in front of DynamoDB.
    /// Extends the ProductController pattern with caching — reads hit cache first,
    /// writes invalidate the cache so the next read fetches fresh data.
    ///
    /// WHY a separate controller over modifying ProductController:
    /// Keeps the caching concern isolated and the comparison between
    /// cached and non-cached paths visible for learning purposes.
    /// In production, cache logic lives in the service layer, not in a
    /// separate controller.
    /// </summary>
    [ApiController]
    [Route("api/cached-products")]
    [Authorize(Policy = AuthConstants.Policies.AuthenticatedUser)]
    public sealed class CachedProductController : ControllerBase
    {
        private readonly ProductService _productService;
        private readonly CacheService _cacheService;
        private readonly ILogger<CachedProductController> _logger;

        /// <summary>
        /// Initialises with both ProductService (DynamoDB) and CacheService (ElastiCache).
        /// The controller coordinates between cache and data layers.
        /// </summary>
        public CachedProductController(
            ProductService productService,
            CacheService cacheService,
            ILogger<CachedProductController> logger)
        {
            _productService = productService;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Returns a product with cache-aside and stampede protection.
        /// Cache key: product:item:{productId}
        /// TTL: 10 minutes (see CacheConstants.Ttl.ProductItem)
        ///
        /// On cache miss: GetOrSetAsync fetches from DynamoDB and caches the result.
        /// Subsequent requests within the TTL window are served from ElastiCache.
        /// </summary>
        [HttpGet("{productId}")]
        public async Task<IActionResult> GetProduct(
            string productId,
            CancellationToken ct)
        {
            var cacheKey = string.Format(
                CacheConstants.Keys.ProductItem, productId);

            var product = await _cacheService.GetOrSetAsync<ProductItem>(
                cacheKey,
                valueFactory: async token => (ProductItem?)await _productService.GetProductAsync(productId, token),
                ttl: CacheConstants.Ttl.ProductItem,
                ct: ct);

            if (product is null)
                return NotFound(new { Error = $"Product '{productId}' not found." });

            return Ok(product);
        }

        /// <summary>
        /// Returns all products in a category with cache-aside and stampede protection.
        /// Cache key: product:category:{categoryName}
        /// TTL: 5 minutes (see CacheConstants.Ttl.ProductCategory)
        /// </summary>
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetProductsByCategory(
            string category,
            CancellationToken ct)
        {
            var cacheKey = string.Format(
                CacheConstants.Keys.ProductCategory, category.ToUpperInvariant());

            var products = await _cacheService.GetOrSetAsync<List<ProductItem>>(
                cacheKey,
                valueFactory: async token =>
                {
                    var result = await _productService
                        .GetProductsByCategoryAsync(category, token);
                    return result.ToList();
                },
                ttl: CacheConstants.Ttl.ProductCategory,
                ct: ct);

            return Ok(products ?? []);
        }

        /// <summary>
        /// Creates a product in DynamoDB and invalidates the category cache.
        /// WHY invalidate category cache on create:
        /// The category listing includes this product — after creation,
        /// the cached listing is stale. Invalidation forces the next
        /// category read to fetch fresh data from DynamoDB.
        /// </summary>
        [HttpPost]
        [Authorize(Policy = AuthConstants.Policies.AdminOnly)]
        public async Task<IActionResult> CreateProduct(
            [FromBody] ProductItem product,
            CancellationToken ct)
        {
            await _productService.SaveProductAsync(product, ct);

            // Invalidate category cache — the listing now includes the new product.
            var categoryCacheKey = string.Format(
                CacheConstants.Keys.ProductCategory,
                product.Category.ToUpperInvariant());

            await _cacheService.InvalidateAsync(categoryCacheKey, ct);

            _logger.LogInformation(
                "Product created and category cache invalidated. " +
                "ProductId={ProductId} Category={Category}",
                product.ProductId, product.Category);

            return CreatedAtAction(
                nameof(GetProduct),
                new { productId = product.ProductId },
                new { product.ProductId, product.Name });
        }

        /// <summary>
        /// Deletes a product from DynamoDB and invalidates both the product
        /// item cache and the category listing cache.
        /// </summary>
        [HttpDelete("{productId}")]
        [Authorize(Policy = AuthConstants.Policies.AdminOnly)]
        public async Task<IActionResult> DeleteProduct(
            string productId,
            CancellationToken ct)
        {
            // Fetch first to get category for cache invalidation.
            var product = await _productService.GetProductAsync(productId, ct);

            await _productService.DeleteProductAsync(productId, ct);

            // Invalidate product item cache.
            var productCacheKey = string.Format(
                CacheConstants.Keys.ProductItem, productId);
            await _cacheService.InvalidateAsync(productCacheKey, ct);

            // Invalidate category listing cache if we know the category.
            if (product is not null)
            {
                var categoryCacheKey = string.Format(
                    CacheConstants.Keys.ProductCategory,
                    product.Category.ToUpperInvariant());
                await _cacheService.InvalidateAsync(categoryCacheKey, ct);
            }

            return NoContent();
        }
    }
}
