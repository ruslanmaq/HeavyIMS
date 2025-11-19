using System;

namespace HeavyIMS.Domain.ValueObjects
{
    /// <summary>
    /// Value Object: DateRange
    /// DEMONSTRATES: DDD Value Object for time periods
    ///
    /// VALUE OBJECT CHARACTERISTICS:
    /// - Immutable: Date range cannot be modified after creation
    /// - No Identity: Two date ranges with same start/end are equal
    /// - Self-Validating: Enforces business rules (start before end)
    /// - Side-Effect Free: Operations return new instances
    ///
    /// WHY VALUE OBJECT?
    /// - Date range is a complex concept, not just two dates
    /// - Encapsulates business rules (start <= end)
    /// - Provides useful operations (duration, overlap, contains)
    /// - Prevents duplicated date logic throughout codebase
    ///
    /// BUSINESS CONTEXT:
    /// - Used for scheduling work order periods
    /// - Scheduled time: When work is planned to occur
    /// - Actual time: When work actually occurred
    /// - Important for tracking delays, accuracy of estimates
    ///
    /// BENEFITS:
    /// - Domain clarity: Code works with DateRange, not separate dates
    /// - Business rules: Start/End validation in one place
    /// - Operations: Duration, overlap, contains encapsulated
    /// - Type safety: Can't accidentally swap start/end parameters
    /// </summary>
    public sealed class DateRange : IEquatable<DateRange>
    {
        /// <summary>
        /// Start date/time of the range
        /// </summary>
        public DateTime Start { get; private init; }

        /// <summary>
        /// End date/time of the range
        /// </summary>
        public DateTime End { get; private init; }

        /// <summary>
        /// Private constructor for EF Core
        /// </summary>
        private DateRange()
        {
        }

        /// <summary>
        /// Factory Method: Create DateRange with validation
        /// DEMONSTRATES: Value Object creation with business rules
        /// </summary>
        public static DateRange Create(DateTime start, DateTime end)
        {
            // BUSINESS RULE: Start must be before or equal to end
            if (start > end)
                throw new ArgumentException(
                    $"Start date ({start}) must be before or equal to end date ({end})",
                    nameof(start));

            return new DateRange
            {
                Start = start,
                End = end
            };
        }

        /// <summary>
        /// Factory Method: Create open-ended range (start with no end)
        /// USEFUL FOR: Work orders started but not completed
        /// </summary>
        public static DateRange CreateOpenEnded(DateTime start)
        {
            // Use DateTime.MaxValue to represent "no end date yet"
            return new DateRange
            {
                Start = start,
                End = DateTime.MaxValue
            };
        }

        /// <summary>
        /// Calculate duration of the date range
        /// USEFUL FOR: Work order duration, time tracking, reporting
        /// </summary>
        public TimeSpan Duration()
        {
            if (End == DateTime.MaxValue)
            {
                // Open-ended range: Calculate duration from start to now
                return DateTime.UtcNow - Start;
            }

            return End - Start;
        }

        /// <summary>
        /// Get duration in hours (common for labor tracking)
        /// USEFUL FOR: Labor hour reports, billing
        /// </summary>
        public decimal DurationInHours()
        {
            return (decimal)Duration().TotalHours;
        }

        /// <summary>
        /// Get duration in days
        /// USEFUL FOR: Project timelines, scheduling
        /// </summary>
        public int DurationInDays()
        {
            return (int)Duration().TotalDays;
        }

        /// <summary>
        /// Check if this range contains a specific date
        /// USEFUL FOR: Checking if work order overlaps with date
        /// </summary>
        public bool Contains(DateTime date)
        {
            return date >= Start && date <= End;
        }

        /// <summary>
        /// Check if this range overlaps with another range
        /// USEFUL FOR: Scheduling conflicts, technician availability
        /// CRITICAL FOR: Preventing double-booking
        /// </summary>
        public bool OverlapsWith(DateRange other)
        {
            // Two ranges overlap if:
            // - This range starts before other ends, AND
            // - This range ends after other starts
            return Start <= other.End && End >= other.Start;
        }

        /// <summary>
        /// Check if this range is completely within another range
        /// USEFUL FOR: Checking if work order fits within available time
        /// </summary>
        public bool IsWithin(DateRange other)
        {
            return Start >= other.Start && End <= other.End;
        }

        /// <summary>
        /// Check if this is an open-ended range (no end date)
        /// </summary>
        public bool IsOpenEnded()
        {
            return End == DateTime.MaxValue;
        }

        /// <summary>
        /// Check if the date range is in the past
        /// USEFUL FOR: Historical data, completed work orders
        /// </summary>
        public bool IsInPast()
        {
            return End < DateTime.UtcNow;
        }

        /// <summary>
        /// Check if the date range is in the future
        /// USEFUL FOR: Upcoming scheduled work
        /// </summary>
        public bool IsInFuture()
        {
            return Start > DateTime.UtcNow;
        }

        /// <summary>
        /// Check if the date range includes the current time
        /// USEFUL FOR: Active work orders
        /// </summary>
        public bool IsCurrentlyActive()
        {
            var now = DateTime.UtcNow;
            return Start <= now && End >= now;
        }

        /// <summary>
        /// Extend the end date of this range
        /// DEMONSTRATES: Immutability - returns new instance
        /// USEFUL FOR: Work order delays, time extensions
        /// </summary>
        public DateRange ExtendTo(DateTime newEnd)
        {
            if (newEnd < End)
                throw new ArgumentException(
                    "New end date must be after current end date",
                    nameof(newEnd));

            return Create(Start, newEnd);
        }

        /// <summary>
        /// VALUE OBJECT EQUALITY: Based on start and end dates
        /// Two date ranges are equal if they have the same start and end
        /// </summary>
        public bool Equals(DateRange? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Start == other.Start && End == other.End;
        }

        public override bool Equals(object? obj)
        {
            return obj is DateRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Start, End);
        }

        public static bool operator ==(DateRange? left, DateRange? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(DateRange? left, DateRange? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// String representation for logging/debugging
        /// </summary>
        public override string ToString()
        {
            if (IsOpenEnded())
                return $"{Start:yyyy-MM-dd HH:mm} - (ongoing)";

            return $"{Start:yyyy-MM-dd HH:mm} - {End:yyyy-MM-dd HH:mm} ({DurationInHours():F1} hours)";
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. WHY DATERANGE AS VALUE OBJECT?
 *    - Date ranges are common domain concept in scheduling
 *    - Encapsulates validation (start before end)
 *    - Provides useful operations (duration, overlap, contains)
 *    - More expressive than two separate DateTime properties
 *
 * 2. BUSINESS RULES ENCODED:
 *    - Start must be <= End (temporal consistency)
 *    - Duration calculations standardized
 *    - Overlap detection for scheduling conflicts
 *    - Open-ended ranges for incomplete work
 *
 * 3. SCHEDULING USE CASES:
 *    - Work order scheduled time (planned)
 *    - Work order actual time (what happened)
 *    - Technician availability windows
 *    - Equipment maintenance windows
 *    - Prevents double-booking via OverlapsWith()
 *
 * 4. IMMUTABILITY IN PRACTICE:
 *    - To change date range, create new instance
 *    - ExtendTo() returns new DateRange
 *    - Prevents bugs from unexpected modifications
 *    - Thread-safe for concurrent operations
 *
 * 5. ALTERNATIVE DESIGNS:
 *    - Could use NodaTime for better date/time handling
 *    - Could include timezone information
 *    - Could include recurrence patterns (future enhancement)
 *    - Current design: Simple UTC-based date ranges
 *
 * 6. EF CORE MAPPING:
 *    - Owned Entity: Maps as two columns (Start, End)
 *    - Column names: ScheduledPeriod_Start, ScheduledPeriod_End
 *    - Or: ActualPeriod_Start, ActualPeriod_End
 *    - Composite index on (Start, End) for range queries
 *
 * 7. QUERY PERFORMANCE:
 *    - Range queries can be expensive
 *    - Consider indexes on Start and End separately
 *    - For "find overlapping ranges": Use Start/End indexes
 *    - SQL Server 2016+: Use built-in OVERLAPS if needed
 */
