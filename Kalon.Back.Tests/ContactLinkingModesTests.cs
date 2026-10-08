using Kalon.Back.Models;

namespace Kalon.Back.Tests;

public class ContactLinkingModesTests
{
    [Theory]
    [InlineData(ContactLinkingModes.None, true)]
    [InlineData(ContactLinkingModes.SameId, true)]
    [InlineData(ContactLinkingModes.SameAddress, true)]
    [InlineData(ContactLinkingModes.Manual, true)]
    [InlineData("invalid", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsValid_ReturnsExpectedResult(string? value, bool expected)
    {
        Assert.Equal(expected, ContactLinkingModes.IsValid(value));
    }

    [Theory]
    [InlineData(ContactLinkingModes.None, false)]
    [InlineData(ContactLinkingModes.SameId, true)]
    [InlineData(ContactLinkingModes.SameAddress, true)]
    [InlineData(ContactLinkingModes.Manual, true)]
    [InlineData("invalid", false)]
    public void IsEnabled_ReturnsExpectedResult(string? value, bool expected)
    {
        Assert.Equal(expected, ContactLinkingModes.IsEnabled(value));
    }

    [Fact]
    public void All_ContainsExactlyFourModes()
    {
        Assert.Equal(4, ContactLinkingModes.All.Count);
        Assert.Contains(ContactLinkingModes.None, ContactLinkingModes.All);
        Assert.Contains(ContactLinkingModes.SameId, ContactLinkingModes.All);
        Assert.Contains(ContactLinkingModes.SameAddress, ContactLinkingModes.All);
        Assert.Contains(ContactLinkingModes.Manual, ContactLinkingModes.All);
    }
}
