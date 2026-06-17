namespace Smart_Home.Service
{
    /// <summary>Abstraction over the hashing algorithm so PIN hashing is mockable/assertable in tests.</summary>
    public interface IPasswordHasher
    {
        string Hash(string raw);
        bool Verify(string raw, string hash);
    }

    /// <summary>BCrypt implementation — the only place in app code that references BCrypt after the refactor.</summary>
    public class BcryptPasswordHasher : IPasswordHasher
    {
        public string Hash(string raw) => BCrypt.Net.BCrypt.HashPassword(raw);
        public bool Verify(string raw, string hash) => BCrypt.Net.BCrypt.Verify(raw, hash);
    }
}
