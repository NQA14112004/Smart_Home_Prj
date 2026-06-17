using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public interface IAlertService
    {
        Task<List<Alert>> GetAlertsAsync(DateTime? startDate, DateTime? endDate);
    }

    public class AlertService : IAlertService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        public AlertService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<Alert>> GetAlertsAsync(DateTime? startDate, DateTime? endDate)
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var query = ctx.Alerts
                .Include(a => a.Node)
                .AsNoTracking()
                .AsQueryable();

            if (startDate.HasValue)
            {
                var start = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
                query = query.Where(a => a.CreatedAt >= start);
            }
            if (endDate.HasValue)
            {
                var end = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                query = query.Where(a => a.CreatedAt < end);
            }

            return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
        }
    }
}
