using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public interface IAccessLogService
    {
        Task<List<AccessLog>> GetLogsAsync(DateTime? startDate, DateTime? endDate);
    }

    public class AccessLogService : IAccessLogService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        public AccessLogService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<AccessLog>> GetLogsAsync(DateTime? startDate, DateTime? endDate)
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var query = ctx.AccessLogs
                .Include(l => l.User)
                .Include(l => l.Node)
                .AsNoTracking()
                .AsQueryable();

            if (startDate.HasValue)
            {
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
                query = query.Where(l => l.CreatedAt >= start);
            }
            if (endDate.HasValue)
            {
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                query = query.Where(l => l.CreatedAt < end);
            }

            return await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        }
    }
}
