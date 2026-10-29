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
    [InlineData("W269N-WFGWX-YVC9B-4J6C9-T83GX", WindowsEdition.Pro)]   // KMS-ключ Windows 10/11 Pro
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
        const string key = "23467-BCDFG-HJKMP-QRTVW-XY23N";

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

    [Theory]
    [InlineData('0')]   // цифры 0, 1, 5 в ключах не используются
    [InlineData('1')]
    [InlineData('5')]
    [InlineData('A')]   // и буквы A, E, I, L, O, S, U, Z
    [InlineData('E')]
    [InlineData('I')]
    [InlineData('L')]
    [InlineData('O')]
    [InlineData('S')]
    [InlineData('U')]
    [InlineData('Z')]
    [InlineData('!')]
    [InlineData('#')]
    public void FormatAsYouType_DropsForbiddenCharacters(char forbidden)
    {
        string formatted = ProductKeyValidator.FormatAsYouType("23467" + forbidden + "BCDFG");

        Assert.Equal("23467-BCDFG", formatted);
        Assert.DoesNotContain(char.ToUpperInvariant(forbidden), formatted);
    }

    [Theory]
    [InlineData('N')]   // буква N пришла в ключи с Windows 8 и должна вводиться
    [InlineData('2')]
    [InlineData('B')]
    [InlineData('Y')]
    public void FormatAsYouType_KeepsAllowedCharacters(char allowed)
    {
        string formatted = ProductKeyValidator.FormatAsYouType("23467" + allowed);

        Assert.Equal("23467-" + char.ToUpperInvariant(allowed), formatted);
    }

    [Fact]
    public void FormatAsYouType_StopsAt25Characters()
    {
        string tooLong = ProductKeyValidator.FormatAsYouType(new string('2', 40) + "BCDFGHJKMNPQRTVWXY23467");

        Assert.Equal(29, tooLong.Length);        // 25 знаков + 4 дефиса
        Assert.Equal("22222-22222-22222-22222-22222", tooLong);
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
