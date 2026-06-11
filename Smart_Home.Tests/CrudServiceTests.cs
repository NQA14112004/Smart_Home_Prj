using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Smart_Home.Models;
using Smart_Home.Service;
using Xunit;

namespace Smart_Home.Tests
{
    public class UserServiceTests
    {
        private static async Task<(IUserService Service, InMemoryDbContextFactory Factory, int RoleId)> CreateAsync()
        {
            var factory = TestData.NewFactory();
            await using var ctx = factory.CreateDbContext();
            var role = new Role { RoleName = "admin" };
            ctx.Roles.Add(role);
            await ctx.SaveChangesAsync();
            return (new UserService(factory), factory, role.Id);
        }

        [Fact]
        public async Task CreateUserAsync_WithBlankFullName_Fails()
        {
            var (service, _, roleId) = await CreateAsync();
            var result = await service.CreateUserAsync("   ", "user1", roleId);
            Assert.False(result.Success);
            Assert.Equal("Vui lòng điền đầy đủ thông tin.", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateUserAsync_WithValidData_PersistsTrimmedActiveUser()
        {
            var (service, factory, roleId) = await CreateAsync();
            var result = await service.CreateUserAsync("  Le Van B  ", "  lvb  ", roleId);
            Assert.True(result.Success);

            await using var ctx = factory.CreateDbContext();
            var user = await ctx.Users.SingleAsync(u => u.Username == "lvb");
            Assert.Equal("Le Van B", user.FullName);
            Assert.Equal("active", user.Status);
        }

        [Fact]
        public async Task CreateUserAsync_DuplicateUsername_Fails()
        {
            var (service, _, roleId) = await CreateAsync();
            await service.CreateUserAsync("A", "dup", roleId);
            var result = await service.CreateUserAsync("B", "dup", roleId);
            Assert.False(result.Success);
            Assert.Equal("Tên đăng nhập đã tồn tại.", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateUserAsync_NotFound_Fails()
        {
            var (service, _, roleId) = await CreateAsync();
            var result = await service.UpdateUserAsync(999, "X", "x", roleId);
            Assert.False(result.Success);
            Assert.Equal("Không tìm thấy người dùng.", result.ErrorMessage);
        }

        [Fact]
        public async Task UpdateUserAsync_DuplicateUsername_Fails()
        {
            var (service, factory, roleId) = await CreateAsync();
            await service.CreateUserAsync("A", "aaa", roleId);
            await service.CreateUserAsync("B", "bbb", roleId);

            await using var ctx = factory.CreateDbContext();
            var b = await ctx.Users.SingleAsync(u => u.Username == "bbb");
            var result = await service.UpdateUserAsync(b.Id, "B", "aaa", roleId);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateUserAsync_Valid_Updates()
        {
            var (service, factory, roleId) = await CreateAsync();
            await service.CreateUserAsync("A", "aaa", roleId);

            long id;
            await using (var ctx = factory.CreateDbContext())
                id = (await ctx.Users.SingleAsync(u => u.Username == "aaa")).Id;

            Assert.True((await service.UpdateUserAsync(id, "Alpha", "alpha", roleId)).Success);

            await using (var verify = factory.CreateDbContext())
                Assert.True(await verify.Users.AnyAsync(u => u.Username == "alpha" && u.FullName == "Alpha"));
        }

        [Fact]
        public async Task SoftDeleteUserAsync_SetsDeleted_AndExcludesFromActive()
        {
            var (service, factory, roleId) = await CreateAsync();
            await service.CreateUserAsync("A", "aaa", roleId);

            long id;
            await using (var ctx = factory.CreateDbContext())
                id = (await ctx.Users.SingleAsync(u => u.Username == "aaa")).Id;

            Assert.True((await service.SoftDeleteUserAsync(id)).Success);
            Assert.DoesNotContain(await service.GetActiveUsersAsync(), u => u.Id == id);
        }

        [Fact]
        public async Task SoftDeleteUserAsync_NotFound_Fails()
        {
            var (service, _, _) = await CreateAsync();
            Assert.False((await service.SoftDeleteUserAsync(12345)).Success);
        }

        [Fact]
        public async Task GetUsersAsync_IncludesRole()
        {
            var (service, _, roleId) = await CreateAsync();
            await service.CreateUserAsync("A", "aaa", roleId);
            var user = Assert.Single(await service.GetUsersAsync());
            Assert.NotNull(user.Role);
            Assert.Equal("admin", user.Role!.RoleName);
        }

        [Fact]
        public async Task GetRolesAsync_ReturnsSeededRoles()
        {
            var (service, _, _) = await CreateAsync();
            var roles = await service.GetRolesAsync();
            Assert.Single(roles);
            Assert.Equal("admin", roles[0].RoleName);
        }
    }

    public class PinCodeServiceTests
    {
        private static async Task<(IPinCodeService Service, InMemoryDbContextFactory Factory, long UserId)> CreateAsync()
        {
            var factory = TestData.NewFactory();
            await using var ctx = factory.CreateDbContext();
            var role = new Role { RoleName = "user" };
            ctx.Roles.Add(role);
            await ctx.SaveChangesAsync();
            var user = new User { FullName = "U", Username = "u", RoleId = role.Id, Status = "active" };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            return (new PinCodeService(factory, new FakePasswordHasher()), factory, user.Id);
        }

        [Fact]
        public async Task CreatePinAsync_TooShort_Fails()
        {
            var (service, _, userId) = await CreateAsync();
            Assert.False((await service.CreatePinAsync(userId, "123", true)).Success);
        }

        [Fact]
        public async Task CreatePinAsync_Valid_HashesViaHasher_NeverPlaintext()
        {
            var (service, factory, userId) = await CreateAsync();
            Assert.True((await service.CreatePinAsync(userId, "1234", true)).Success);

            await using var ctx = factory.CreateDbContext();
            var pin = await ctx.PinCodes.SingleAsync();
            Assert.Equal(FakePasswordHasher.Prefix + "1234", pin.PinHash);
            Assert.NotEqual("1234", pin.PinHash);
        }

        [Fact]
        public async Task UpdatePinAsync_NotFound_Fails()
        {
            var (service, _, userId) = await CreateAsync();
            Assert.False((await service.UpdatePinAsync(999, userId, "1234", true)).Success);
        }

        [Fact]
        public async Task UpdatePinAsync_WithoutNewPin_KeepsHash()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreatePinAsync(userId, "1234", true);

            long id;
            string originalHash;
            await using (var ctx = factory.CreateDbContext())
            {
                var p = await ctx.PinCodes.SingleAsync();
                id = p.Id;
                originalHash = p.PinHash;
            }

            Assert.True((await service.UpdatePinAsync(id, userId, null, false)).Success);

            await using (var verify = factory.CreateDbContext())
            {
                var p = await verify.PinCodes.SingleAsync();
                Assert.Equal(originalHash, p.PinHash);
                Assert.False(p.IsActive);
            }
        }

        [Fact]
        public async Task UpdatePinAsync_WithNewPin_RehashesAndResetsLockout()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreatePinAsync(userId, "1234", true);

            long id;
            await using (var ctx = factory.CreateDbContext())
            {
                var p = await ctx.PinCodes.SingleAsync();
                p.FailedAttempts = 5;
                p.LockedUntil = DateTime.UtcNow.AddHours(1);
                await ctx.SaveChangesAsync();
                id = p.Id;
            }

            Assert.True((await service.UpdatePinAsync(id, userId, "5678", true)).Success);

            await using (var verify = factory.CreateDbContext())
            {
                var p = await verify.PinCodes.SingleAsync();
                Assert.Equal(FakePasswordHasher.Prefix + "5678", p.PinHash);
                Assert.Equal(0, p.FailedAttempts);
                Assert.Null(p.LockedUntil);
            }
        }

        [Fact]
        public async Task DeactivatePinAsync_SetsInactive()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreatePinAsync(userId, "1234", true);

            long id;
            await using (var ctx = factory.CreateDbContext())
                id = (await ctx.PinCodes.SingleAsync()).Id;

            Assert.True((await service.DeactivatePinAsync(id)).Success);
            await using (var verify = factory.CreateDbContext())
                Assert.False((await verify.PinCodes.SingleAsync()).IsActive);
        }

        [Fact]
        public async Task GetActiveUsersAsync_ExcludesDeleted()
        {
            var (service, factory, userId) = await CreateAsync();
            await using (var ctx = factory.CreateDbContext())
            {
                ctx.Users.Add(new User { FullName = "Del", Username = "del", RoleId = 1, Status = "deleted" });
                await ctx.SaveChangesAsync();
            }

            var users = await service.GetActiveUsersAsync();
            Assert.DoesNotContain(users, u => u.Username == "del");
            Assert.Contains(users, u => u.Id == userId);
        }

        [Fact]
        public async Task GetPinsAsync_IncludesUser()
        {
            var (service, _, userId) = await CreateAsync();
            await service.CreatePinAsync(userId, "1234", true);
            var pin = Assert.Single(await service.GetPinsAsync());
            Assert.NotNull(pin.User);
        }
    }

    public class RfidCardServiceTests
    {
        private static async Task<(IRfidCardService Service, InMemoryDbContextFactory Factory, long UserId)> CreateAsync()
        {
            var factory = TestData.NewFactory();
            await using var ctx = factory.CreateDbContext();
            var role = new Role { RoleName = "user" };
            ctx.Roles.Add(role);
            await ctx.SaveChangesAsync();
            var user = new User { FullName = "U", Username = "u", RoleId = role.Id, Status = "active" };
            ctx.Users.Add(user);
            await ctx.SaveChangesAsync();
            return (new RfidCardService(factory), factory, user.Id);
        }

        [Fact]
        public async Task CreateCardAsync_BlankUid_Fails()
        {
            var (service, _, userId) = await CreateAsync();
            Assert.False((await service.CreateCardAsync("  ", "label", userId, true)).Success);
        }

        [Fact]
        public async Task CreateCardAsync_Valid_TrimsAndPersists()
        {
            var (service, factory, userId) = await CreateAsync();
            Assert.True((await service.CreateCardAsync("  ABC123  ", "  My Card  ", userId, true)).Success);

            await using var ctx = factory.CreateDbContext();
            var card = await ctx.RfidCards.SingleAsync();
            Assert.Equal("ABC123", card.CardUid);
            Assert.Equal("My Card", card.CardLabel);
            Assert.Null(card.DeactivatedAt);
        }

        [Fact]
        public async Task CreateCardAsync_Inactive_SetsDeactivatedAt()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreateCardAsync("X1", null, userId, false);
            await using var ctx = factory.CreateDbContext();
            Assert.NotNull((await ctx.RfidCards.SingleAsync()).DeactivatedAt);
        }

        [Fact]
        public async Task CreateCardAsync_DuplicateUid_Fails()
        {
            var (service, _, userId) = await CreateAsync();
            await service.CreateCardAsync("DUP", null, userId, true);
            Assert.False((await service.CreateCardAsync("DUP", null, userId, true)).Success);
        }

        [Fact]
        public async Task UpdateCardAsync_NotFound_Fails()
        {
            var (service, _, userId) = await CreateAsync();
            Assert.False((await service.UpdateCardAsync(999, "U", null, userId, true)).Success);
        }

        [Fact]
        public async Task UpdateCardAsync_DuplicateUid_Fails()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreateCardAsync("AAA", null, userId, true);
            await service.CreateCardAsync("BBB", null, userId, true);

            long id;
            await using (var ctx = factory.CreateDbContext())
                id = (await ctx.RfidCards.SingleAsync(c => c.CardUid == "BBB")).Id;

            Assert.False((await service.UpdateCardAsync(id, "AAA", null, userId, true)).Success);
        }

        [Fact]
        public async Task UpdateCardAsync_ActiveToInactive_SetsDeactivatedAt()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreateCardAsync("AAA", null, userId, true);

            long id;
            await using (var ctx = factory.CreateDbContext())
                id = (await ctx.RfidCards.SingleAsync()).Id;

            await service.UpdateCardAsync(id, "AAA", null, userId, false);
            await using (var verify = factory.CreateDbContext())
                Assert.NotNull((await verify.RfidCards.SingleAsync()).DeactivatedAt);
        }

        [Fact]
        public async Task DeactivateCardAsync_SetsInactiveWithTimestamp()
        {
            var (service, factory, userId) = await CreateAsync();
            await service.CreateCardAsync("AAA", null, userId, true);

            long id;
            await using (var ctx = factory.CreateDbContext())
                id = (await ctx.RfidCards.SingleAsync()).Id;

            Assert.True((await service.DeactivateCardAsync(id)).Success);
            await using (var verify = factory.CreateDbContext())
            {
                var card = await verify.RfidCards.SingleAsync();
                Assert.False(card.IsActive);
                Assert.NotNull(card.DeactivatedAt);
            }
        }

        [Fact]
        public async Task GetCardsAsync_IncludesUser()
        {
            var (service, _, userId) = await CreateAsync();
            await service.CreateCardAsync("AAA", null, userId, true);
            var card = Assert.Single(await service.GetCardsAsync());
            Assert.NotNull(card.User);
        }

        [Fact]
        public async Task GetActiveUsersAsync_ExcludesDeleted()
        {
            var (service, factory, userId) = await CreateAsync();
            await using (var ctx = factory.CreateDbContext())
            {
                ctx.Users.Add(new User { FullName = "Del", Username = "del2", RoleId = 1, Status = "deleted" });
                await ctx.SaveChangesAsync();
            }

            var users = await service.GetActiveUsersAsync();
            Assert.DoesNotContain(users, u => u.Username == "del2");
            Assert.Contains(users, u => u.Id == userId);
        }
    }
}
