using AwsCloudNative.Common.Constants;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace AwsCloudNative.Api.Services
{
    /// <summary>
    /// Encapsulates all distributed cache operations with:
    /// - Generic GET/SET with JSON serialisation
    /// - Cache-aside pattern with typed return
    /// - Stampede protection via distributed locking
    ///
    /// WHY a wrapper service over IDistributedCache directly:
    /// IDistributedCache only works with byte arrays and strings.
    /// Every caller would need to serialise/deserialise manually.
    /// CacheService centralises serialisation, TTL configuration,
    /// and stampede protection — controllers and services stay clean.
    /// </summary>
    public sealed class CacheService
    {
        private readonly IDistributedCache _cache;
        private readonly IConnectionMultiplexer _multiplexer;
        private readonly ILogger<CacheService> _logger;

        // Unique token per process instance for lock ownership identification.
        // WHY static readonly: Generated once per process start.
        // Used to verify lock ownership before release —
        // prevents one process from releasing another process's lock.
        private static readonly string LockOwnerToken =
            $"{Environment.MachineName}:{Guid.NewGuid()}";

        /// <summary>
        /// Initialises with IDistributedCache for GET/SET
        /// and IConnectionMultiplexer for distributed lock operations.
        /// </summary>
        public CacheService(
            IDistributedCache cache,
            IConnectionMultiplexer multiplexer,
            ILogger<CacheService> logger)
        {
            _cache = cache;
            _multiplexer = multiplexer;
            _logger = logger;
        }

        /// <summary>
        /// Implements the cache-aside pattern with stampede protection.
        /// 
        /// Flow:
        ///   1. Check cache → HIT: return immediately
        ///   2. Acquire distributed lock → only one caller proceeds to DB
        ///   3. Check cache again (double-check inside lock) →
        ///      another process may have populated it while we waited for the lock
        ///   4. Call valueFactory (DB query)
        ///   5. Store result in cache
        ///   6. Release lock
        ///   7. Return value
        ///
        /// WHY double-check inside lock:
        /// Between step 1 (cache miss) and step 2 (lock acquired),
        /// another process may have already populated the cache.
        /// Without the double-check, every lock waiter still queries the DB.
        /// With double-check, only the first process queries the DB.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="cacheKey">The cache key to check and populate.</param>
        /// <param name="valueFactory">
        /// Async factory called on cache miss — typically a database query.
        /// Only called by ONE process at a time thanks to the distributed lock.
        /// </param>
        /// <param name="ttl">How long to cache the value.</param>
        /// <param name="ct">Cancellation token.</param>
        public async Task<T?> GetOrSetAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T?>> valueFactory,
        TimeSpan ttl,
        CancellationToken ct = default) where T : class
        {
            // ── Step 1: Fast path — check cache first ─────────────────────────
            var cached = await GetAsync<T>(cacheKey, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Cache HIT. Key={Key}", cacheKey);
                return cached;
            }

            _logger.LogDebug("Cache MISS. Key={Key}", cacheKey);

            // ── Step 2: Acquire distributed lock ─────────────────────────────
            var lockKey = $"lock:{cacheKey}";
            var lockAcquired = await AcquireLockAsync(lockKey, ct);

            if (!lockAcquired)
            {
                // Could not acquire lock — another process is populating the cache.
                // Wait briefly and read from cache (should be warm by now).
                // WHY 100ms: Long enough for the lock holder to complete,
                // short enough not to add noticeable latency for the caller.
                await Task.Delay(100, ct);
                var afterWait = await GetAsync<T>(cacheKey, ct);
                if (afterWait is not null) return afterWait;

                // Cache still empty after wait — fall through to DB query
                // as a safety net to avoid returning null.
            }

            try
            {
                // ── Step 3: Double-check inside lock ─────────────────────────
                var doubleCheck = await GetAsync<T>(cacheKey, ct);
                if (doubleCheck is not null)
                {
                    _logger.LogDebug(
                        "Cache populated by another process. Key={Key}", cacheKey);
                    return doubleCheck;
                }

                // ── Step 4: Call factory (database query) ─────────────────────
                var value = await valueFactory(ct);
                if (value is null) return null;

                // ── Step 5: Populate cache ─────────────────────────────────────
                await SetAsync(cacheKey, value, ttl, ct);

                return value;
            }
            finally
            {
                // ── Step 6: Always release lock ────────────────────────────────
                // WHY finally: If the factory throws, the lock must still be
                // released. Without finally, a crash leaves a lock that
                // blocks all callers until LockExpiry TTL expires.
                if (lockAcquired)
                    await ReleaseLockAsync(lockKey);
            }
        }

        /// <summary>
        /// Gets a cached value by key, deserialised from JSON.
        /// Returns null on cache miss or deserialisation failure.
        /// </summary>
        public async Task<T?> GetAsync<T>(
            string cacheKey,
            CancellationToken ct = default) where T : class
        {
            try
            {
                var bytes = await _cache.GetAsync(cacheKey, ct);
                if (bytes is null or { Length: 0 }) return null;

                return JsonSerializer.Deserialize<T>(bytes);
            }
            catch (Exception ex)
            {
                // WHY catch and return null rather than throw:
                // Cache is an optimisation layer — not the source of truth.
                // If Redis is unavailable or returns corrupt data,
                // the app must degrade gracefully by going to the database.
                // Throwing here would take down the API when cache is unhealthy.
                _logger.LogWarning(ex,
                    "Cache GET failed. Key={Key} — falling through to source.", cacheKey);
                return null;
            }
        }

        /// <summary>
        /// Sets a value in cache serialised as JSON with the given TTL.
        /// Failures are logged and swallowed — cache is non-critical path.
        /// </summary>
        public async Task SetAsync<T>(
        string cacheKey,
        T value,
        TimeSpan ttl,
        CancellationToken ct = default) where T : class
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
                var entryOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl
                };

                await _cache.SetAsync(cacheKey, bytes, entryOptions, ct);

                _logger.LogDebug(
                    "Cache SET. Key={Key} TTL={TTL}", cacheKey, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Cache SET failed. Key={Key} — value not cached.", cacheKey);
            }
        }

        /// <summary>
        /// Invalidates (deletes) a cache key.
        /// Called after writes to the database to force the next read
        /// to fetch fresh data. Failures logged and swallowed.
        /// </summary>
        public async Task InvalidateAsync(
            string cacheKey,
            CancellationToken ct = default)
        {
            try
            {
                await _cache.RemoveAsync(cacheKey, ct);
                _logger.LogDebug("Cache INVALIDATED. Key={Key}", cacheKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Cache invalidation failed. Key={Key}", cacheKey);
            }
        }

        /// <summary>
        /// Acquires a distributed lock using Redis SET NX (Set if Not eXists).
        ///
        /// WHY SET NX and not INCR or SETNX:
        /// SET with NX + EX flags is atomic in Redis/Valkey.
        /// It sets the key only if it does not exist AND sets the expiry
        /// in the same atomic operation — no race between SET and EXPIRE.
        /// The old SETNX command required two separate commands (SETNX + EXPIRE)
        /// which could leave an unexpiring lock if the process crashed between them.
        /// </summary>
        private async Task<bool> AcquireLockAsync(
            string lockKey,
            CancellationToken ct)
        {
            try
            {
                var db = _multiplexer.GetDatabase();

                // SET lockKey LockOwnerToken NX EX {seconds}
                // NX = only set if Not eXists (atomic compare-and-set)
                // EX = expire automatically after LockExpiry seconds
                return await db.StringSetAsync(
                    lockKey,
                    LockOwnerToken,
                    CacheConstants.Ttl.LockExpiry,
                    When.NotExists);  // NX flag
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to acquire distributed lock. Key={Key} — proceeding without lock.",
                    lockKey);

                // If lock acquisition fails (Redis unavailable),
                // return false — caller will still serve the request
                // via the fallback path (no stampede protection, but functional).
                return false;
            }
        }

        /// <summary>
        /// Releases the distributed lock using a Lua script for atomic check-and-delete.
        ///
        /// WHY Lua script instead of just DELETE:
        /// A plain DELETE would delete the lock even if another process owns it.
        /// Scenario: Process A's lock expires. Process B acquires the lock.
        /// Process A finishes its work and calls DELETE — deletes Process B's lock.
        /// The Lua script checks the owner token before deleting — only the
        /// owner that set the lock can delete it.
        /// </summary>
        private async Task ReleaseLockAsync(string lockKey)
        {
            try
            {
                var db = _multiplexer.GetDatabase();

                // Lua script: check owner, delete only if this process owns the lock.
                // Executes atomically on the Redis server — no race condition possible.
                const string releaseScript = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

                await db.ScriptEvaluateAsync(
                    releaseScript,
                    keys: [(RedisKey)lockKey],
                    values: [(RedisValue)LockOwnerToken]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to release distributed lock. Key={Key} — will expire automatically.",
                    lockKey);
                // Lock will expire via TTL — not a fatal error.
            }
        }
    }
}
