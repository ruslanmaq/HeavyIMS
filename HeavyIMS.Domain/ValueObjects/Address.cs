using System;

namespace HeavyIMS.Domain.ValueObjects
{
    /// <summary>
    /// Value Object: Address
    /// DEMONSTRATES: DDD Value Object pattern for complex types
    ///
    /// VALUE OBJECT CHARACTERISTICS:
    /// - Immutable: Once created, cannot be modified
    /// - No Identity: Two addresses with same values are equal
    /// - Structural Equality: Compared by value, not reference
    /// - Self-Validating: Enforces address format rules
    ///
    /// WHY VALUE OBJECT?
    /// - Address is a descriptive concept with no identity
    /// - "123 Main St, Dallas, TX 75001" is always equal to another identical address
    /// - Encapsulates address validation and formatting in one place
    /// - Prevents scattered string manipulation throughout codebase
    ///
    /// BENEFITS:
    /// - Domain clarity: Code explicitly works with Address, not raw strings
    /// - Validation: Address rules enforced at creation
    /// - Formatting: ToString() provides consistent formatting
    /// - Refactoring: Easy to change address structure without breaking code
    /// </summary>
    public sealed class Address : IEquatable<Address>
    {
        /// <summary>
        /// Street address line 1 (required)
        /// </summary>
        public string Street { get; private init; }

        /// <summary>
        /// Street address line 2 (optional - apt, suite, etc.)
        /// </summary>
        public string? Street2 { get; private init; }

        /// <summary>
        /// City name (required)
        /// </summary>
        public string City { get; private init; }

        /// <summary>
        /// State/Province code (required)
        /// </summary>
        public string State { get; private init; }

        /// <summary>
        /// ZIP/Postal code (required)
        /// </summary>
        public string ZipCode { get; private init; }

        /// <summary>
        /// Country code (defaults to USA)
        /// </summary>
        public string Country { get; private init; }

        /// <summary>
        /// Private constructor for EF Core
        /// </summary>
        private Address()
        {
            Street = string.Empty;
            City = string.Empty;
            State = string.Empty;
            ZipCode = string.Empty;
            Country = "USA";
        }

        /// <summary>
        /// Factory Method: Create Address with validation
        /// DEMONSTRATES: Value Object creation with business rules
        /// </summary>
        public static Address Create(
            string street,
            string city,
            string state,
            string zipCode,
            string? street2 = null,
            string country = "USA")
        {
            // BUSINESS RULES: Required fields validation
            if (string.IsNullOrWhiteSpace(street))
                throw new ArgumentException("Street address is required", nameof(street));

            if (string.IsNullOrWhiteSpace(city))
                throw new ArgumentException("City is required", nameof(city));

            if (string.IsNullOrWhiteSpace(state))
                throw new ArgumentException("State is required", nameof(state));

            if (string.IsNullOrWhiteSpace(zipCode))
                throw new ArgumentException("ZIP code is required", nameof(zipCode));

            // BUSINESS RULE: US ZIP code format validation (basic)
            if (country == "USA" && !IsValidUSZipCode(zipCode))
                throw new ArgumentException(
                    "Invalid US ZIP code format. Expected: 12345 or 12345-6789",
                    nameof(zipCode));

            return new Address
            {
                Street = street.Trim(),
                Street2 = string.IsNullOrWhiteSpace(street2) ? null : street2.Trim(),
                City = city.Trim(),
                State = state.Trim().ToUpperInvariant(),
                ZipCode = zipCode.Trim(),
                Country = country.Trim().ToUpperInvariant()
            };
        }

        /// <summary>
        /// Helper: Validate US ZIP code format
        /// Accepts: 12345 or 12345-6789
        /// </summary>
        private static bool IsValidUSZipCode(string zipCode)
        {
            if (string.IsNullOrWhiteSpace(zipCode))
                return false;

            var zip = zipCode.Trim();

            // Basic format: 5 digits or 5 digits + hyphen + 4 digits
            if (zip.Length == 5)
                return zip.All(char.IsDigit);

            if (zip.Length == 10 && zip[5] == '-')
                return zip.Substring(0, 5).All(char.IsDigit) &&
                       zip.Substring(6, 4).All(char.IsDigit);

            return false;
        }

        /// <summary>
        /// Get full address as single line
        /// USEFUL FOR: Mailing labels, displays
        /// </summary>
        public string GetFullAddress()
        {
            var parts = new[]
            {
                Street,
                Street2,
                $"{City}, {State} {ZipCode}",
                Country != "USA" ? Country : null
            };

            return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }

        /// <summary>
        /// Get multi-line formatted address
        /// USEFUL FOR: Invoices, letters
        /// </summary>
        public string GetMultiLineAddress()
        {
            var lines = new[]
            {
                Street,
                Street2,
                $"{City}, {State} {ZipCode}",
                Country != "USA" ? Country : null
            };

            return string.Join(Environment.NewLine,
                lines.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        /// <summary>
        /// VALUE OBJECT EQUALITY: Based on all properties
        /// Two addresses are equal if all components match
        /// </summary>
        public bool Equals(Address? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Street.Equals(other.Street, StringComparison.OrdinalIgnoreCase) &&
                   (Street2 ?? "").Equals(other.Street2 ?? "", StringComparison.OrdinalIgnoreCase) &&
                   City.Equals(other.City, StringComparison.OrdinalIgnoreCase) &&
                   State.Equals(other.State, StringComparison.OrdinalIgnoreCase) &&
                   ZipCode.Equals(other.ZipCode, StringComparison.OrdinalIgnoreCase) &&
                   Country.Equals(other.Country, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is Address other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Street.ToUpperInvariant(),
                (Street2 ?? "").ToUpperInvariant(),
                City.ToUpperInvariant(),
                State.ToUpperInvariant(),
                ZipCode.ToUpperInvariant(),
                Country.ToUpperInvariant());
        }

        public static bool operator ==(Address? left, Address? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Address? left, Address? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// String representation (single line format)
        /// </summary>
        public override string ToString()
        {
            return GetFullAddress();
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. WHY ADDRESS AS VALUE OBJECT?
 *    - No identity: Address doesn't need a unique ID
 *    - Descriptive: Describes where something is, not what it is
 *    - Reusable: Same address structure everywhere (Customer, Warehouse, etc.)
 *    - Immutable: To change address, replace entire Address object
 *
 * 2. ALTERNATIVE: Address as separate table/entity
 *    - Use Entity if: Need address history, address sharing, geocoding data
 *    - Use Value Object if: Address is just a property, no separate lifecycle
 *    - For this system: Address is property of Customer (Value Object)
 *
 * 3. VALIDATION STRATEGY:
 *    - Basic validation at creation (required fields, format)
 *    - Could extend: Real-time address verification API
 *    - Could extend: International address formats
 *    - Could extend: Address autocomplete/suggestion
 *
 * 4. FORMATTING METHODS:
 *    - GetFullAddress(): One-line for displays
 *    - GetMultiLineAddress(): Multi-line for printing
 *    - Encapsulates formatting logic in one place
 *    - Easy to change formatting without touching entity code
 *
 * 5. EF CORE MAPPING:
 *    - Owned Entity: Maps all properties to parent table
 *    - Column names: Address_Street, Address_City, etc.
 *    - No separate AddressId or foreign key needed
 */
