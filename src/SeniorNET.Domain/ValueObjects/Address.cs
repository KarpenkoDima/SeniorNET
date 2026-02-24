namespace SeniorNET.Domain.ValueObjects;

public sealed record Address
{
    public string Street { get; }
    public string City { get; }
    public string ZipCode { get; }
    public string Country { get; }

    // EF Core requires parameterless constructor
    private Address() { Street = City = ZipCode = Country = string.Empty; }

    private Address(string street, string city, string zipCode, string country)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
        Country = country;
    }

    public static Address Create(string street, string city, string zipCode, string country)
    {
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street is required.", nameof(street));
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City is required.", nameof(city));
        if (string.IsNullOrWhiteSpace(zipCode))
            throw new ArgumentException("Zip code is required.", nameof(zipCode));
        if (string.IsNullOrWhiteSpace(country))
            throw new ArgumentException("Country is required.", nameof(country));

        return new Address(street.Trim(), city.Trim(), zipCode.Trim(), country.Trim());
    }

    public override string ToString() => $"{Street}, {City}, {ZipCode}, {Country}";
}
