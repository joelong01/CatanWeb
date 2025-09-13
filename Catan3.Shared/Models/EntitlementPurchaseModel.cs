namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents a model for an entitlement purchase, including the entitlement and whether it is enabled.
    /// </summary>
    public class EntitlementPurchaseModel
    {
        /// <summary>
        /// Gets or sets the entitlement.
        /// </summary>
        public Entitlement Entitlement { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is allowed to buy this entitlement at this time.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the EntitlementPurchaseModel class.
        /// </summary>
        public EntitlementPurchaseModel() { }

        /// <summary>
        /// Initializes a new instance of the EntitlementPurchaseModel class with the specified entitlement.
        /// </summary>
        /// <param name="entitlement">The entitlement that will be purchased.</param>
        public EntitlementPurchaseModel(Entitlement entitlement)
        {
            Entitlement = entitlement;
        }
    }
}