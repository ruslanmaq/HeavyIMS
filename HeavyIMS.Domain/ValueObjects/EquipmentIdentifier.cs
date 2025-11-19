using System;

namespace HeavyIMS.Domain.ValueObjects
{
    /// <summary>
    /// Value Object: EquipmentIdentifier
    /// DEMONSTRATES: DDD Value Object for domain-specific identifiers
    ///
    /// VALUE OBJECT CHARACTERISTICS:
    /// - Immutable: Equipment identity cannot change
    /// - No Identity: The VIN itself IS the identifier
    /// - Self-Validating: Enforces VIN format and required fields
    /// - Cohesive: Groups related equipment identification data
    ///
    /// WHY VALUE OBJECT?
    /// - Equipment identification is a complex concept, not just a string
    /// - VIN + Type + Model are always used together
    /// - Encapsulates equipment validation rules
    /// - Prevents spreading VIN validation throughout codebase
    ///
    /// BUSINESS CONTEXT:
    /// - Heavy equipment (excavators, bulldozers, cranes) has VIN like vehicles
    /// - Type: Category of equipment (Excavator, Bulldozer, Crane, etc.)
    /// - Model: Manufacturer model (CAT 320, John Deere 450J, etc.)
    /// - VIN is unique identifier for individual equipment unit
    ///
    /// BENEFITS:
    /// - Type safety: Can't pass raw string where equipment needed
    /// - Domain clarity: Code explicitly works with EquipmentIdentifier
    /// - Validation: VIN format validated once at creation
    /// - Cohesion: Related data grouped together
    /// </summary>
    public sealed class EquipmentIdentifier : IEquatable<EquipmentIdentifier>
    {
        /// <summary>
        /// Vehicle Identification Number (VIN)
        /// Standard 17-character format for heavy equipment
        /// UNIQUE identifier for this specific equipment unit
        /// </summary>
        public string VIN { get; private init; }

        /// <summary>
        /// Equipment type/category
        /// Examples: Excavator, Bulldozer, Loader, Crane, Grader
        /// </summary>
        public string Type { get; private init; }

        /// <summary>
        /// Manufacturer model designation
        /// Examples: "CAT 320", "John Deere 450J", "Komatsu PC200"
        /// </summary>
        public string Model { get; private init; }

        /// <summary>
        /// Private constructor for EF Core
        /// </summary>
        private EquipmentIdentifier()
        {
            VIN = string.Empty;
            Type = string.Empty;
            Model = string.Empty;
        }

        /// <summary>
        /// Factory Method: Create EquipmentIdentifier with validation
        /// DEMONSTRATES: Value Object creation with domain rules
        /// </summary>
        public static EquipmentIdentifier Create(string vin, string type, string model)
        {
            // BUSINESS RULE: VIN is required
            if (string.IsNullOrWhiteSpace(vin))
                throw new ArgumentException("Equipment VIN is required", nameof(vin));

            // BUSINESS RULE: VIN must be 17 characters (standard format)
            var cleanVIN = vin.Trim().ToUpperInvariant();
            if (cleanVIN.Length != 17)
                throw new ArgumentException(
                    "Equipment VIN must be exactly 17 characters",
                    nameof(vin));

            // BUSINESS RULE: VIN must be alphanumeric (no special characters)
            if (!cleanVIN.All(c => char.IsLetterOrDigit(c)))
                throw new ArgumentException(
                    "Equipment VIN must contain only letters and numbers",
                    nameof(vin));

            // BUSINESS RULE: Type is required
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Equipment type is required", nameof(type));

            // BUSINESS RULE: Model is optional but recommended
            // Some equipment might not have specific model designation
            var cleanModel = string.IsNullOrWhiteSpace(model) ? "Unknown" : model.Trim();

            return new EquipmentIdentifier
            {
                VIN = cleanVIN,
                Type = type.Trim(),
                Model = cleanModel
            };
        }

        /// <summary>
        /// Get equipment display name
        /// USEFUL FOR: UI displays, logs, reports
        /// Example: "CAT 320 Excavator (1HGBH41JXMN109186)"
        /// </summary>
        public string GetDisplayName()
        {
            return $"{Model} {Type} ({VIN})";
        }

        /// <summary>
        /// Get short identifier (last 8 digits of VIN)
        /// USEFUL FOR: Quick reference, work order labels
        /// Example: "MN109186"
        /// </summary>
        public string GetShortVIN()
        {
            return VIN.Length >= 8 ? VIN.Substring(VIN.Length - 8) : VIN;
        }

        /// <summary>
        /// Check if this is a specific equipment type
        /// USEFUL FOR: Business rules, pricing, technician specialization
        /// </summary>
        public bool IsType(string equipmentType)
        {
            return Type.Equals(equipmentType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// VALUE OBJECT EQUALITY: Based on VIN (unique identifier)
        /// Two equipment identifiers are equal if they have the same VIN
        /// Note: VIN uniquely identifies the equipment, type/model should match
        /// </summary>
        public bool Equals(EquipmentIdentifier? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            // VIN is the primary identifier
            return VIN.Equals(other.VIN, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is EquipmentIdentifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            return VIN.ToUpperInvariant().GetHashCode();
        }

        public static bool operator ==(EquipmentIdentifier? left, EquipmentIdentifier? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(EquipmentIdentifier? left, EquipmentIdentifier? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// String representation for logging/debugging
        /// </summary>
        public override string ToString()
        {
            return GetDisplayName();
        }
    }
}

/*
 * DESIGN NOTES:
 *
 * 1. WHY EQUIPMENT AS VALUE OBJECT?
 *    - Groups related identification data (VIN, Type, Model)
 *    - VIN validation in one place
 *    - Type safety: Can't pass wrong string to equipment field
 *    - Domain concept: "Equipment" is more meaningful than 3 separate strings
 *
 * 2. VIN VALIDATION:
 *    - Standard 17-character format (ISO 3779)
 *    - Alphanumeric only (letters I, O, Q excluded in real VINs)
 *    - Could extend: Check digit validation (position 9)
 *    - Could extend: Decode VIN (manufacturer, year, plant)
 *
 * 3. ALTERNATIVE: Equipment as Entity
 *    - Use Entity if: Equipment has separate lifecycle, history, maintenance records
 *    - Use Value Object if: Just identifying what equipment for work order
 *    - Current design: Value Object (equipment info is property of WorkOrder)
 *    - Future: Could extract Equipment as aggregate if needed
 *
 * 4. EQUALITY SEMANTICS:
 *    - Based on VIN only (unique identifier)
 *    - Type and Model should match if VIN matches (data integrity)
 *    - If VINs match but Type/Model differ, data corruption issue
 *
 * 5. BUSINESS RULES ENCODED:
 *    - VIN required and exactly 17 characters
 *    - Type required (what kind of equipment)
 *    - Model optional but recommended
 *    - Validation happens at creation (fail fast)
 *
 * 6. EF CORE MAPPING:
 *    - Owned Entity: Maps to parent table columns
 *    - Column names: Equipment_VIN, Equipment_Type, Equipment_Model
 *    - Could add index on VIN for lookups
 */
