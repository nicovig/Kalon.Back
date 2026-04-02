using System;
using System.Collections.Generic;
using BCrypt.Net;
using Kalon.Back.Services;
using Microsoft.Extensions.Options;
using Xunit;

public class PasswordServiceTests
{
    private static PasswordService CreateService(string pepper, int iterations = 120000, int hashSize = 32)
    {
        var options = new PasswordOptions
        {
            Pepper = pepper,
            Iterations = iterations,
            HashSize = hashSize
        };

        return new PasswordService(Options.Create(options));
    }

    [Fact]
    public void VerifyPassword_Pbkdf2_Roundtrip_ReturnsTrue()
    {
        var service = CreateService("viser_lindependance_financiere_002");
        var password = "Password123!";
        var salt = "c2FsdA==";

        var hash = service.HashPassword(password, salt);

        Assert.True(service.VerifyPassword(password, hash, salt));
    }

    [Fact]
    public void VerifyPassword_Pbkdf2_WrongPassword_ReturnsFalse()
    {
        var service = CreateService("viser_lindependance_financiere_002");
        var password = "Password123!";
        var salt = "c2FsdA==";
        var hash = service.HashPassword(password, salt);

        Assert.False(service.VerifyPassword("WrongPassword!", hash, salt));
    }

    [Fact]
    public void VerifyPassword_Pbkdf2_TrimsInputs()
    {
        var service = CreateService("viser_lindependance_financiere_002");
        var password = "Password123!";
        var salt = "c2FsdA==";
        var hash = service.HashPassword(password, salt);

        Assert.True(service.VerifyPassword($" {password} ", $" {hash} ", $" {salt} "));
    }

    [Fact]
    public void VerifyPassword_Bcrypt_DetectsHashFormat()
    {
        var service = CreateService("irrelevant_pepper");
        var password = "Password123!";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);

        Assert.True(service.VerifyPassword(password, hash, "any-salt"));
        Assert.False(service.VerifyPassword("WrongPassword!", hash, "any-salt"));
    }
}

