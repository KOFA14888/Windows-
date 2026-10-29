using Win10Sim.Core.Setup;
using Xunit;

namespace Win10Sim.Core.Tests;

public class ProductKeyValidatorTests
{
    [Theory]
    [InlineData("VK7JG-NPHTM-C97JM-9MPGT-3V66T", WindowsEdition.Pro)]
    [InlineData("YTMG3-N6DKC-DKB77-7M9GH-8HVX7", WindowsEdition.Home)]
    [InlineData("NPPR9-FWDCX-D2C8J-H872K-2YT43", WindowsEdition.Enterprise)]
    [InlineData("vk7jg-nphtm-c97jm-9mpgt-3v66t", WindowsEdition.Pro)]
    public void GenericKeys_MapToKnownEditions(string key, WindowsEdition expected)
    {
        ProductKeyValidation result = ProductKeyValidator.Validate(key);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Edition);
        Assert.Equal(expected, ProductKeyValidator.Validate(key.ToUpperInvariant()).Edition);
    }

    [Fact]
    public void Normalize_AddsDashesAndUppercases()
    {
        Assert.Equal("VK7JG-NPHTM-C97JM-9MPGT-3V66T",
            ProductKeyValidator.Normalize("vk7 jg nphtm c97jm 9mpgt 3v66t"));
    }

    [Theory]
    [InlineData("", ProductKeyError.Empty)]
    [InlineData("   ", ProductKeyError.Empty)]
    [InlineData("VK7JG", ProductKeyError.WrongLength)]
    [InlineData("VK7JG-NPHTM-C97JM-9MPGT-3V66T-EXTRA", ProductKeyError.WrongLength)]
    [InlineData("VK7JG-NPHTM-C97JM-9MPGT-3V66O", ProductKeyError.InvalidCharacters)]
    [InlineData("VK7JG-NPHTM-C97JM-9MPGT-3V66I", ProductKeyError.InvalidCharacters)]
    public void InvalidKeys_AreRejectedWithSpecificError(string key, ProductKeyError expected)
    {
        ProductKeyValidation result = ProductKeyValidator.Validate(key);

        Assert.False(result.IsValid);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void RandomKey_GetsSomeEdition_Deterministically()
    {
        const string key = "23456-BCDFG-HJKMP-QRTVW-XY234";

        ProductKeyValidation first = ProductKeyValidator.Validate(key);
        ProductKeyValidation second = ProductKeyValidator.Validate(key);

        Assert.True(first.IsValid);
        Assert.Equal(first.Edition, second.Edition);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("vk7jg", "VK7JG")]
    [InlineData("VK7JGNPHTMC97JM9MPGT3V66T", "VK7JG-NPHTM-C97JM-9MPGT-3V66T")]
    [InlineData("VK7JG-NPHTM-C97JM-9MPGT-3V66T", "VK7JG-NPHTM-C97JM-9MPGT-3V66T")]
    public void FormatAsYouType_GroupsByFive(string input, string expected)
    {
        Assert.Equal(expected, ProductKeyValidator.FormatAsYouType(input));
    }

    [Fact]
    public void FormatAsYouType_DropsForbiddenCharactersAndOverflow()
    {
        // I, O, S, B, L, 0, 1 и посторонние символы не должны попадать в поле.
        string formatted = ProductKeyValidator.FormatAsYouType("ab1IOS0!9cd-efgh");

        Assert.DoesNotContain("I", formatted);
        Assert.DoesNotContain("O", formatted);
        Assert.DoesNotContain("S", formatted);
        Assert.DoesNotContain("B", formatted);
        Assert.DoesNotContain("L", formatted);
        Assert.DoesNotContain("0", formatted);
        Assert.DoesNotContain("1", formatted);
        Assert.DoesNotContain("!", formatted);

        string tooLong = ProductKeyValidator.FormatAsYouType(new string('2', 40) + "BCDFGHJKMPQRTVWXY23456");
        Assert.Equal(29, tooLong.Length);        // 25 знаков + 4 дефиса
    }

    [Fact]
    public void SampleGenericKey_IsAlwaysValid()
    {
        foreach (WindowsEdition edition in Enum.GetValues<WindowsEdition>())
        {
            ProductKeyValidation result = ProductKeyValidator.Validate(
                ProductKeyValidator.SampleGenericKey(edition));

            Assert.True(result.IsValid);
            Assert.Equal(edition, result.Edition);
        }
    }
}
