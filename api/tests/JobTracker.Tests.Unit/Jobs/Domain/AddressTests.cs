using Jobs.Domain.Jobs;

namespace JobTracker.Tests.Unit.Jobs.Domain;

public sealed class AddressTests
{
    [Theory]
    [InlineData("", "Miami", "FL", "33101")]
    [InlineData("123 Main", "", "FL", "33101")]
    [InlineData("123 Main", "Miami", "", "33101")]
    [InlineData("123 Main", "Miami", "FL", "")]
    [InlineData("123 Main", "Miami", "FL", "AB")]
    [InlineData("123 Main", "Miami", "FL", "AB@12")]
    [InlineData("123 Main", "Miami", "FL", "12345678901")]
    public void Create_returns_validation_error_when_any_required_field_is_missing_or_zip_is_invalid(
        string street, string city, string state, string zip)
    {
        var result = Address.Create(street, city, state, zip);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(JobTracker.SharedKernel.Results.ErrorType.Validation);
    }

    [Theory]
    [InlineData("33101")]
    [InlineData("33101-1234")]
    [InlineData("110111")]
    [InlineData("K1A 0B1")]
    [InlineData("SW1A 1AA")]
    [InlineData("C1425AAB")]
    [InlineData("A65 F4E2")]
    public void Create_accepts_valid_international_postal_codes(string zip)
    {
        var result = Address.Create("123 Main", "Miami", "FL", zip);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_latitude_out_of_range()
    {
        var result = Address.Create("123 Main", "Miami", "FL", "33101", latitude: 91m);
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Address.InvalidLatitude");
    }

    [Fact]
    public void Two_addresses_with_same_components_are_structurally_equal()
    {
        var a = Address.Create("123 Main", "Miami", "FL", "33101").Value!;
        var b = Address.Create("123 Main", "Miami", "FL", "33101").Value!;

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Create_trims_whitespace_from_string_fields()
    {
        var address = Address.Create("  123 Main  ", "  Miami ", " FL ", " 33101 ").Value!;

        address.Street.Should().Be("123 Main");
        address.City.Should().Be("Miami");
        address.State.Should().Be("FL");
        address.ZipCode.Should().Be("33101");
    }
}
