using System;

namespace HeavyIMS.Domain.ValueObjects
{
    /// <summary>
    /// Value Object: Money
    /// DEMONSTRATES: DDD Value Object pattern
    ///
    /// VALUE OBJECT CHARACTERISTICS:
    /// - Immutable: Cannot be changed after creation
    /// - No Identity: Two Money objects with same amount/currency are equal
    /// - Self-Validating: Enforces business rules in constructor
    /// - Side-Effect Free: Operations return new instances
    ///
    /// WHY VALUE OBJECT?
    /// - Money is a descriptive concept with no identity
    /// - $100 USD is always equal to another $100 USD (no unique identity needed)
    /// - Encapsulates money validation and operations in one place
    /// - Prevents primitive obsession (using raw decimal everywhere)
    ///
    /// BENEFITS:
    /// - Type safety: Can't accidentally add Price + Quantity
    /// - Domain clarity: Code reads like business language
    /// - Centralized validation: Money rules in one place
    /// - Thread safe: Immutability prevents race conditions
    /// </summary>
    public sealed class Money : IEquatable<Money>
    {
        /// <summary>
        /// The monetary amount
        /// </summary>
        public decimal Amount { get; private init; }

        /// <summary>
        /// Currency code (ISO 4217 format: USD, EUR, etc.)
        /// For this system, we default to USD
        /// </summary>
        public string Currency { get; private init; }

        /// <summary>
        /// Private constructor for EF Core
        /// </summary>
        private Money()
        {
            Currency = "USD";
        }

        /// <summary>
        /// Factory Method: Create Money with validation
        /// DEMONSTRATES: Value Object creation pattern
        /// </summary>
        public static Money Create(decimal amount, string currency = "USD")
        {
            // BUSINESS RULE: Amount cannot be negative (for this domain)
            // Note: Some domains allow negative money (debts, refunds)
            if (amount < 0)
                throw new ArgumentException("Money amount cannot be negative", nameof(amount));

            // BUSINESS RULE: Currency must be specified
            if (string.IsNullOrWhiteSpace(currency))
                throw new ArgumentException("Currency is required", nameof(currency));

            return new Money { Amount = amount, Currency = currency.ToUpperInvariant() };
        }

        /// <summary>
        /// Factory Method: Create zero money
        /// USEFUL FOR: Default values, initializations
        /// </summary>
        public static Money Zero(string currency = "USD")
        {
            return new Money { Amount = 0, Currency = currency };
        }

        /// <summary>
        /// Add two Money values
        /// DEMONSTRATES: Value Object operations
        /// </summary>
        public Money Add(Money other)
        {
            if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Cannot add money with different currencies: {Currency} and {other.Currency}");

            return Create(Amount + other.Amount, Currency);
        }

        /// <summary>
        /// Subtract two Money values
        /// </summary>
        public Money Subtract(Money other)
        {
            if (!Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Cannot subtract money with different currencies: {Currency} and {other.Currency}");

            return Create(Amount - other.Amount, Currency);
        }

        /// <summary>
        /// Multiply money by a factor
        /// USEFUL FOR: Calculating totals (quantity × price)
        /// </summary>
        public Money Multiply(decimal factor)
        {
            if (factor < 0)
                throw new ArgumentException("Multiplication factor cannot be negative", nameof(factor));

            return Create(Amount * factor, Currency);
        }

        /// <summary>
        /// Operator overload: Addition
        /// DEMONSTRATES: Making Value Objects work like primitives
        /// </summary>
        public static Money operator +(Money left, Money right) => left.Add(right);

        /// <summary>
        /// Operator overload: Subtraction
        /// </summary>
        public static Money operator -(Money left, Money right) => left.Subtract(right);

        /// <summary>
        /// Operator overload: Multiplication
        /// </summary>
        public static Money operator *(Money money, decimal factor) => money.Multiply(factor);

        /// <summary>
        /// Comparison: Greater than
        /// </summary>
        public static bool operator >(Money left, Money right)
        {
            if (!left.Currency.Equals(right.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot compare money with different currencies");

            return left.Amount > right.Amount;
        }

        /// <summary>
        /// Comparison: Less than
        /// </summary>
        public static bool operator <(Money left, Money right)
        {
            if (!left.Currency.Equals(right.Currency, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot compare money with different currencies");

            return left.Amount < right.Amount;
        }

        /// <summary>
        /// VALUE OBJECT EQUALITY: Based on content, not identity
        /// Two Money objects are equal if they have same amount and currency
        /// </summary>
        public bool Equals(Money? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Amount == other.Amount &&
                   Currency.Equals(other.Currency, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is Money other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Amount, Currency.ToUpperInvariant());
        }

        public static bool operator ==(Money? left, Money? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Money? left, Money? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// String representation for logging/debugging
        /// </summary>
        public override string ToString()
        {
            return $"{Amount:N2} {Currency}";
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. WHY VALUE OBJECTS?
 *    - Primitive obsession: Using raw decimal everywhere loses domain meaning
 *    - Type safety: Money + Money is valid, Money + Quantity is not
 *    - Encapsulation: All money rules in one place
 *    - Immutability: Thread-safe, prevents bugs
 *
 * 2. VALUE OBJECT vs ENTITY:
 *    - Entity: Has identity (Customer #123 vs Customer #456)
 *    - Value Object: No identity ($100 = $100, no unique ID needed)
 *    - Entity: Mutable lifecycle (customer info changes)
 *    - Value Object: Immutable (to change $100, create new Money)
 *
 * 3. IMMUTABILITY BENEFITS:
 *    - Thread safety: Can share across threads safely
 *    - Predictability: Value never changes unexpectedly
 *    - Caching: Safe to cache immutable values
 *    - Debugging: Value at creation = value forever
 *
 * 4. OPERATOR OVERLOADING:
 *    - Makes Value Objects feel like primitives
 *    - More readable: `price + tax` vs `price.Add(tax)`
 *    - But be careful: Can make code less explicit
 *
 * 5. EF CORE MAPPING:
 *    - Owned Entity: Maps as complex type in same table
 *    - Value Conversion: Maps to single column
 *    - We'll use Owned Entity for full Money structure
 */
