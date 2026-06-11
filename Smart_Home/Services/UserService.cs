using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Data;
using Smart_Home.Models;

namespace Smart_Home.Service
{
    public interface IUserService
    {
        Task<List<Role>> GetRolesAsync();
        Task<List<User>> GetUsersAsync();
        Task<List<User>> GetActiveUsersAsync();
        Task<OperationResult> CreateUserAsync(string fullName, string username, int roleId);
        Task<OperationResult> UpdateUserAsync(long id, string fullName, string username, int roleId);
        Task<OperationResult> SoftDeleteUserAsync(long id);
    }

    public class UserService : IUserService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        public UserService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<Role>> GetRolesAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Roles.AsNoTracking().ToListAsync();
        }

        public async Task<List<User>> GetUsersAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Users.Include(u => u.Role).AsNoTracking().ToListAsync();
        }

        public async Task<List<User>> GetActiveUsersAsync()
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            return await ctx.Users.Where(u => u.Status != "deleted").AsNoTracking().ToListAsync();
        }

        public async Task<OperationResult> CreateUserAsync(string fullName, string username, int roleId)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
                return OperationResult.Fail("Vui lòng điền đầy đủ thông tin.");

            var name = username.Trim();
            await using var ctx = await _factory.CreateDbContextAsync();
            if (await ctx.Users.AnyAsync(u => u.Username == name))
                return OperationResult.Fail("Tên đăng nhập đã tồn tại.");

            ctx.Users.Add(new User
            {
                FullName = fullName.Trim(),
                Username = name,
                RoleId = roleId,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> UpdateUserAsync(long id, string fullName, string username, int roleId)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(username))
                return OperationResult.Fail("Vui lòng điền đầy đủ thông tin.");

            var name = username.Trim();
            await using var ctx = await _factory.CreateDbContextAsync();
            if (await ctx.Users.AnyAsync(u => u.Id != id && u.Username == name))
                return OperationResult.Fail("Tên đăng nhập đã tồn tại.");

            var user = await ctx.Users.FindAsync(id);
            if (user == null) return OperationResult.Fail("Không tìm thấy người dùng.");

            user.FullName = fullName.Trim();
            user.Username = name;
            user.RoleId = roleId;
            user.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }

        public async Task<OperationResult> SoftDeleteUserAsync(long id)
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            var user = await ctx.Users.FindAsync(id);
            if (user == null) return OperationResult.Fail("Không tìm thấy người dùng.");

            user.Status = "deleted";
            user.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return OperationResult.Ok();
        }
    }
}
