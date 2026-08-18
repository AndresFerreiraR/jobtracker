using System.Text.RegularExpressions;
using JobTracker.SharedKernel.Primitives;
using JobTracker.SharedKernel.Results;

namespace Jobs.Domain.Jobs;

public sealed class Address : ValueObject
{
    private static readonly Regex ZipCodeRegex =
        new(@"^[A-Za-z0-9][A-Za-z0-9\- ]{2,9}$", RegexOptions.Compiled);

    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string ZipCode { get; }
    public decimal? Latitude { get; }
    public decimal? Longitude { get; }

    private Address(
        string street,
        string city,
        string state,
        string zipCode,
        decimal? latitude,
        decimal? longitude)
    {
        Street = street;
        City = city;
        State = state;
        ZipCode = zipCode;
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Result<Address> Create(
        string? street,
        string? city,
        string? state,
        string? zipCode,
        decimal? latitude = null,
        decimal? longitude = null)
    {
        if (string.IsNullOrWhiteSpace(street)) return AddressErrors.StreetRequired;
        if (string.IsNullOrWhiteSpace(city))   return AddressErrors.CityRequired;
        if (string.IsNullOrWhiteSpace(state))  return AddressErrors.StateRequired;
        if (string.IsNullOrWhiteSpace(zipCode)) return AddressErrors.InvalidZipCode;

        var trimmedZip = zipCode.Trim();
        if (!ZipCodeRegex.IsMatch(trimmedZip)) return AddressErrors.InvalidZipCode;

        if (latitude is < -90 or > 90)   return AddressErrors.InvalidLatitude;
        if (longitude is < -180 or > 180) return AddressErrors.InvalidLongitude;

        return new Address(
            street.Trim(),
            city.Trim(),
            state.Trim(),
            trimmedZip,
            latitude,
            longitude);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return ZipCode;
        yield return Latitude;
        yield return Longitude;
    }
}
