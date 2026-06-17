using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public interface IRfidCardService
    {
        Task<List<User>> GetActiveUsersAsync();
        Task<List<RfidCard>> GetCardsAsync();
        Task<OperationResult> CreateCardAsync(string cardUid, string? cardLabel, long userId, bool isActive);
        Task<OperationResult> UpdateCardAsync(long id, string cardUid, string? cardLabel, long userId, bool isActive);
        Task<OperationResult> DeactivateCardAsync(long id);
    }

    public class RfidCardService : IRfidCardService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        public RfidCardService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<User>> GetActiveUsersAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Users.Where(u => u.Status != "deleted").AsNoTracking().ToListAsync();
        }

        public async Task<List<RfidCard>> GetCardsAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.RfidCards.Include(c => c.User).AsNoTracking().ToListAsync();
        }

        public async Task<OperationResult> CreateCardAsync(string cardUid, string? cardLabel, long userId, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(cardUid))
                return OperationResult.Fail("Vui lòng nhập UID thẻ.");

            var uid = cardUid.Trim();
            await using var ctx = await _factory.CreateDbContextAsync();
            if (await ctx.RfidCards.AnyAsync(c => c.CardUid == uid))
                return OperationResult.Fail("UID thẻ đã tồn tại trong hệ thống.");

            ctx.RfidCards.Add(new RfidCard
            {
                CardUid = uid,
                CardLabel = string.IsNullOrWhiteSpace(cardLabel) ? null : cardLabel.Trim(),
                UserId = userId,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                DeactivatedAt = isActive ? null : DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> UpdateCardAsync(long id, string cardUid, string? cardLabel, long userId, bool isActive)
        {
            var uid = cardUid.Trim();
            await using var ctx = await _factory.CreateDbContextAsync();
            var card = await ctx.RfidCards.FindAsync(id);
            if (card == null) return OperationResult.Fail("Không tìm thấy thẻ.");

            if (await ctx.RfidCards.AnyAsync(c => c.Id != id && c.CardUid == uid))
                return OperationResult.Fail("UID thẻ đã tồn tại trong hệ thống.");

            var wasActive = card.IsActive;
            card.CardUid = uid;
            card.CardLabel = string.IsNullOrWhiteSpace(cardLabel) ? null : cardLabel.Trim();
            card.UserId = userId;
            card.IsActive = isActive;
            card.UpdatedAt = DateTime.UtcNow;

            if (wasActive && !isActive) card.DeactivatedAt = DateTime.UtcNow;
            else if (!wasActive && isActive) card.DeactivatedAt = null;

            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> DeactivateCardAsync(long id)
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var card = await ctx.RfidCards.FindAsync(id);
            if (card == null) return OperationResult.Fail("Không tìm thấy thẻ.");

            card.IsActive = false;
            card.DeactivatedAt = DateTime.UtcNow;
            card.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }
    }
}
