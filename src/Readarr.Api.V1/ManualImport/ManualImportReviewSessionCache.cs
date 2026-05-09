using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NLog;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;

namespace Readarr.Api.V1.ManualImport
{
    public interface IManualImportReviewSessionCache
    {
        ManualImportReviewSessionResult<ManualImportPageResult> GetManualImportPage(string folder, string downloadId, NzbDrone.Core.Books.Author author, FilterFilesType filter, bool replaceExistingFiles, int page, int pageSize, bool refresh, Func<ManualImportPageResult> factory);
        ManualImportReviewSessionResult<List<ManualImportItem>> GetUnmappedReviewItems(List<BookFile> files, bool replaceExistingFiles, bool refresh, Func<List<ManualImportItem>> factory);
    }

    public class ManualImportReviewSessionResult<T>
    {
        public string SessionKey { get; set; }
        public bool FromCache { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public T Value { get; set; }
    }

    public class ManualImportReviewSessionCache : IManualImportReviewSessionCache
    {
        private const int MaxEntries = 32;
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new ConcurrentDictionary<string, CacheEntry>();
        private readonly object _cleanupLock = new object();
        private readonly Logger _logger;

        public ManualImportReviewSessionCache(Logger logger)
        {
            _logger = logger;
        }

        public ManualImportReviewSessionResult<ManualImportPageResult> GetManualImportPage(string folder, string downloadId, NzbDrone.Core.Books.Author author, FilterFilesType filter, bool replaceExistingFiles, int page, int pageSize, bool refresh, Func<ManualImportPageResult> factory)
        {
            var key = BuildKey("manual-page", new[]
            {
                Normalize(folder),
                Normalize(downloadId),
                author?.Id.ToString() ?? "0",
                filter.ToString(),
                replaceExistingFiles.ToString(),
                Math.Max(page, 1).ToString(),
                Math.Max(pageSize, 1).ToString()
            });

            return GetOrAdd(key, refresh, factory);
        }

        public ManualImportReviewSessionResult<List<ManualImportItem>> GetUnmappedReviewItems(List<BookFile> files, bool replaceExistingFiles, bool refresh, Func<List<ManualImportItem>> factory)
        {
            var fileScope = files
                .OrderBy(x => x.Id)
                .Select(x => string.Join("|", x.Id, Normalize(x.Path), x.Size, x.Modified.Ticks, x.Reviewed))
                .ToList();

            fileScope.Insert(0, replaceExistingFiles.ToString());

            var key = BuildKey("unmapped-page", fileScope);

            return GetOrAdd(key, refresh, factory);
        }

        private ManualImportReviewSessionResult<T> GetOrAdd<T>(string key, bool refresh, Func<T> factory)
        {
            var now = DateTime.UtcNow;

            if (!refresh && _cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now && cached.Value is T cachedValue)
            {
                _logger.Debug("Manual import review session cache hit: {0}", key);
                return new ManualImportReviewSessionResult<T>
                {
                    SessionKey = key,
                    FromCache = true,
                    CreatedAt = cached.CreatedAt,
                    ExpiresAt = cached.ExpiresAt,
                    Value = cachedValue
                };
            }

            var value = factory();
            var entry = new CacheEntry
            {
                CreatedAt = now,
                ExpiresAt = now.Add(Ttl),
                Value = value
            };

            _cache[key] = entry;
            TrimExpiredAndOverflow(now);

            _logger.Debug("Manual import review session cache {0}: {1}", refresh ? "refresh" : "miss", key);

            return new ManualImportReviewSessionResult<T>
            {
                SessionKey = key,
                FromCache = false,
                CreatedAt = entry.CreatedAt,
                ExpiresAt = entry.ExpiresAt,
                Value = value
            };
        }

        private void TrimExpiredAndOverflow(DateTime now)
        {
            if (_cache.Count <= MaxEntries && !_cache.Any(x => x.Value.ExpiresAt <= now))
            {
                return;
            }

            lock (_cleanupLock)
            {
                foreach (var key in _cache.Where(x => x.Value.ExpiresAt <= now).Select(x => x.Key).ToList())
                {
                    _cache.TryRemove(key, out _);
                }

                foreach (var key in _cache.OrderBy(x => x.Value.CreatedAt).Take(Math.Max(0, _cache.Count - MaxEntries)).Select(x => x.Key).ToList())
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        private static string BuildKey(string prefix, IEnumerable<string> parts)
        {
            var raw = string.Join("\n", parts.Select(x => x ?? string.Empty));
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

            return $"{prefix}:{Convert.ToHexString(hash).ToLowerInvariant()}";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private class CacheEntry
        {
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public object Value { get; set; }
        }
    }
}
