using Districauchos.Core.Accounts;
using Xunit;

namespace Districauchos.Core.Tests;

public class AccountTests
{
    [Fact]
    public void Hash_verifies_only_the_same_password()
    {
        var h = Passwords.Hash("Repuestos2026");
        Assert.StartsWith("pbkdf2-sha512$210000$", h);
        Assert.True(Passwords.Verify("Repuestos2026", h));
        Assert.False(Passwords.Verify("repuestos2026", h));
        Assert.NotEqual(h, Passwords.Hash("Repuestos2026")); // sal distinta cada vez
    }

    [Theory]
    [InlineData("")]
    [InlineData("pbkdf2-sha512$abc$x$y")]
    [InlineData("md5$1$2$3")]
    [InlineData("pbkdf2-sha512$210000$no-base64$no-base64")]
    public void Verify_rejects_malformed_hashes(string stored) => Assert.False(Passwords.Verify("x", stored));

    [Theory]
    [InlineData("corta", true)]
    [InlineData("12345678", true)]
    [InlineData("aaaaaaaa", true)]
    [InlineData("password1", true)]
    [InlineData("juanperez99", true)] // contiene el correo
    [InlineData("Mangueras-Aveo", false)]
    public void Problem_flags_weak_passwords(string password, bool weak) =>
        Assert.Equal(weak, Passwords.Problem(password, "juanperez@gmail.com") is not null);

    [Fact]
    public void Tokens_are_random_url_safe_and_hash_to_32_bytes()
    {
        var a = AccountTokens.New();
        Assert.True(AccountTokens.LooksValid(a));
        Assert.NotEqual(a, AccountTokens.New());
        Assert.Equal(32, AccountTokens.HashOf(a).Length);
        Assert.False(AccountTokens.LooksValid("corto"));
        Assert.False(AccountTokens.LooksValid(new string('a', 40) + "/"));
    }
}
