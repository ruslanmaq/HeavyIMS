using System;
using System.Collections.Generic;
using System.Linq;
using HeavyIMS.Domain.ValueObjects;

namespace HeavyIMS.Domain.Entities
{
    /// <summary>
    /// Domain Entity: Customer
    /// Represents a customer who owns equipment requiring service
    /// ADDRESSES CHALLENGE 3: Communication with customers
    ///
    /// DDD Pattern: AGGREGATE ROOT
    /// - Independent lifecycle (exists separately from work orders)
    /// - Consistency boundary for customer data and notification preferences
    /// - Referenced by WorkOrder aggregate via CustomerId
    /// - Primary business entity representing client relationships
    ///
    /// Why it's an Aggregate Root:
    /// 1. Independent Identity: Customers exist before and after work orders
    /// 2. Transaction Boundary: Customer info and preferences must be consistent
    /// 3. External Reference Point: Other aggregates reference Customer by ID
    /// 4. Business Invariants: Company name required, active status enforced
    ///
    /// What it manages:
    /// - Customer master data (company, contact, address)
    /// - Communication preferences for notifications
    /// - Active/inactive status for business operations
    ///
    /// Cross-Aggregate References:
    /// - WorkOrders reference Customer by ID only (no navigation property - DDD principle)
    /// - To get customer's work orders, query WorkOrder aggregate with CustomerId filter
    /// </summary>
    public class Customer : AggregateRoot
    {
        public Guid Id { get; private set; }
        public string CompanyName { get; private set; }
        public string ContactName { get; private set; }
        public string Email { get; private set; }
        public string PhoneNumber { get; private set; }

        // Address (VALUE OBJECT)
        // BEFORE: Single string
        // AFTER: Address value object with street, city, state, zip
        public Address Address { get; private set; }

        // Communication preferences (CHALLENGE 3)
        public bool PreferEmailNotifications { get; private set; }
        public bool PreferSMSNotifications { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public bool IsActive { get; private set; }

        private Customer()
        {
        }

        /// <summary>
        /// Factory Method: Create new customer (backwards compatible version)
        /// DEMONSTRATES: Controlled object creation with validation
        /// Note: This overload accepts string address for backwards compatibility
        /// </summary>
        public static Customer Create(string companyName, string contactName,
            string email, string phoneNumber, string address)
        {
            // Parse address string into components (simple parsing for compatibility)
            // Format expected: "123 Main St, Dallas, TX, 75001"
            var addressValue = ParseAddressFromString(address);

            return Create(companyName, contactName, email, phoneNumber, addressValue);
        }

        /// <summary>
        /// Factory Method: Create new customer with Address value object
        /// DEMONSTRATES: Value Object usage in entity creation
        /// </summary>
        public static Customer Create(string companyName, string contactName,
            string email, string phoneNumber, Address address)
        {
            // Business rule validation
            if (string.IsNullOrWhiteSpace(companyName))
                throw new ArgumentException("Company name is required", nameof(companyName));

            if (address == null)
                throw new ArgumentNullException(nameof(address));

            return new Customer
            {
                Id = Guid.NewGuid(),
                CompanyName = companyName,
                ContactName = contactName,
                Email = email,
                PhoneNumber = phoneNumber,
                Address = address,
                PreferEmailNotifications = true,
                PreferSMSNotifications = true,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        /// <summary>
        /// Helper: Parse address from string (backwards compatibility)
        /// </summary>
        private static Address ParseAddressFromString(string addressString)
        {
            if (string.IsNullOrWhiteSpace(addressString))
            {
                // Default address if not provided
                return Address.Create("Unknown", "Unknown", "TX", "00000");
            }

            // Simple parsing: assume format "Street, City, State, Zip"
            var parts = addressString.Split(',').Select(p => p.Trim()).ToArray();

            if (parts.Length >= 4)
            {
                return Address.Create(parts[0], parts[1], parts[2], parts[3]);
            }
            else
            {
                // Fallback: treat entire string as street address
                return Address.Create(addressString, "Unknown", "TX", "00000");
            }
        }

        /// <summary>
        /// Domain Method: Update notification preferences
        /// BUSINESS RULE: Controls how customer receives work order updates
        /// USED BY: Notification system to determine delivery method
        /// </summary>
        public void UpdateNotificationPreferences(bool preferEmail, bool preferSMS)
        {
            PreferEmailNotifications = preferEmail;
            PreferSMSNotifications = preferSMS;
        }

        /// <summary>
        /// Domain Method: Update customer contact information
        /// </summary>
        public void UpdateContactInfo(string contactName, string email, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required", nameof(email));

            ContactName = contactName;
            Email = email;
            PhoneNumber = phoneNumber;
        }

        /// <summary>
        /// Domain Method: Update customer address (backwards compatible)
        /// </summary>
        public void UpdateAddress(string address)
        {
            Address = ParseAddressFromString(address);
        }

        /// <summary>
        /// Domain Method: Update customer address with Address value object
        /// USES VALUE OBJECTS: Address for structured address data
        /// </summary>
        public void UpdateAddress(Address address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));

            Address = address;
        }

        /// <summary>
        /// Domain Method: Deactivate customer
        /// BUSINESS RULE: Inactive customers cannot create new work orders
        /// </summary>
        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// Domain Method: Reactivate customer
        /// </summary>
        public void Reactivate()
        {
            IsActive = true;
        }
    }
}
