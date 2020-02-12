using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Cardknox
{
    public class CardknoxPaymentSettings : ISettings
    {
        public TransactMode TransactMode { get; set; }
        public string TransactionKey { get; set; }
        public string SoftwareName { get; set; }
        public string SoftwareVersion { get; set; }

        /// <summary>
        /// Overrides the default API version sent by the Cardknox SDK
        /// </summary>
        public bool OverrideApiVersion { get; set; }

        /// <summary>
        /// Api Version to send to the Cardknox SDK
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Doesn't send the address details to Cardknox
        /// </summary>
        public bool HideAddressDetails { get; set; }

        /// <summary>
        /// Uses the shipping address as the billing address
        /// </summary>
        public bool UseShippingAddressAsBilling { get; set; }

        /// <summary>
        /// Sends an receipt to the customer e-mail
        /// </summary>
        public bool SendReceiptToCustomerEmail { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }

        /// <summary>
        /// Additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }
    }
}