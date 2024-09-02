using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CloudWebApp.Services
{
    public class MemoryCacheTicketStore : ITicketStore
    {
        private const string KeyPrefix = "AuthTicket_";
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<MemoryCacheTicketStore> _logger;

        public MemoryCacheTicketStore(IMemoryCache memoryCache, ILogger<MemoryCacheTicketStore> logger)
        {
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var guid = Guid.NewGuid().ToString();
            var key = KeyPrefix + guid;
            await RenewAsync(key, ticket);
            _logger.LogInformation($"Stored ticket with key: {key}");
            return guid;
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            var options = new MemoryCacheEntryOptions();
            var expiresUtc = ticket.Properties.ExpiresUtc;
            if (expiresUtc.HasValue)
            {
                options.SetAbsoluteExpiration(expiresUtc.Value);
            }
            _memoryCache.Set(KeyPrefix + key, ticket, options);
            _logger.LogInformation($"Renewed ticket with key: {KeyPrefix + key}");
            return Task.CompletedTask;
        }

        public Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            _memoryCache.TryGetValue(KeyPrefix + key, out AuthenticationTicket ticket);
            _logger.LogInformation($"Retrieved ticket with key: {KeyPrefix + key}, Found: {ticket != null}");
            return Task.FromResult(ticket);
        }

        public Task RemoveAsync(string key)
        {
            _memoryCache.Remove(KeyPrefix + key);
            _logger.LogInformation($"Removed ticket with key: {KeyPrefix + key}");
            return Task.CompletedTask;
        }
    }
}
