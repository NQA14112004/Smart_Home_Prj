using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public interface IPinCodeService
    {
        Task<List<User>> GetActiveUsersAsync();
        Task<List<PinCode>> GetPinsAsync();
        Task<OperationResult> CreatePinAsync(long userId, string rawPin, bool isActive);
        Task<OperationResult> UpdatePinAsync(long id, long userId, string? rawPin, bool isActive);
        Task<OperationResult> DeactivatePinAsync(long id);
    }

    public class PinCodeService : IPinCodeService
    {
        public const int MinPinLength = 4;

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IPasswordHasher _hasher;

        public PinCodeService(IDbContextFactory<AppDbContext> factory, IPasswordHasher hasher)
        {
            _factory = factory;
            _hasher = hasher;
        }

        public async Task<List<User>> GetActiveUsersAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Users.Where(u => u.Status != "deleted").AsNoTracking().ToListAsync();
        }

        public async Task<List<PinCode>> GetPinsAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.PinCodes.Include(p => p.User).AsNoTracking().ToListAsync();
        }

        public async Task<OperationResult> CreatePinAsync(long userId, string rawPin, bool isActive)
        {
            if (!IsValidPin(rawPin))
                return OperationResult.Fail($"Mã PIN phải có ít nhất {MinPinLength} ký tự.");

            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.PinCodes.Add(new PinCode
            {
                UserId = userId,
                PinHash = _hasher.Hash(rawPin),
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> UpdatePinAsync(long id, long userId, string? rawPin, bool isActive)
        {
            var changePin = !string.IsNullOrWhiteSpace(rawPin);
            if (changePin && !IsValidPin(rawPin!))
                return OperationResult.Fail($"Mã PIN mới phải có ít nhất {MinPinLength} ký tự.");

            await using var ctx = await _factory.CreateDbContextAsync();
            var pin = await ctx.PinCodes.FindAsync(id);
            if (pin == null) return OperationResult.Fail("Không tìm thấy mã PIN.");

            pin.UserId = userId;
            pin.IsActive = isActive;
            pin.UpdatedAt = DateTime.UtcNow;

            if (changePin)
            {
                pin.PinHash = _hasher.Hash(rawPin!);
                pin.FailedAttempts = 0;
                pin.LockedUntil = null;
            }

            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> DeactivatePinAsync(long id)
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var pin = await ctx.PinCodes.FindAsync(id);
            if (pin == null) return OperationResult.Fail("Không tìm thấy mã PIN.");

            pin.IsActive = false;
            pin.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        private static bool IsValidPin(string raw) =>
            !string.IsNullOrWhiteSpace(raw) && raw.Trim().Length >= MinPinLength;
    }
}
