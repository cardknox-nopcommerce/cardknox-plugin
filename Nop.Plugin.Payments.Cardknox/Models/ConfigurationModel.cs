using Nop.Web.Framework;
using Nop.Web.Framework.Mvc;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.Cardknox.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.TransactModeValues")]
        public int TransactModeId { get; set; }

        public bool TransactModeId_OverrideForStore { get; set; }
        public SelectList TransactModeValues { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.TransactionKey")]
        public string TransactionKey { get; set; }

        public bool TransactionKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.SoftwareName")]
        public string SoftwareName { get; set; }

        public bool SoftwareName_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.SoftwareVersion")]
        public string SoftwareVersion { get; set; }

        public bool SoftwareVersion_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.SoftwareApiVersion")]
        public string SoftwareApiVersion { get; set; }

        public bool SoftwareApiVersion_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.UseCustomSoftwareApiVersion")]
        public bool UseCustomSoftwareApiVersion { get; set; }

        public bool UseCustomSoftwareApiVersion_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }

        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }

        public bool AdditionalFeePercentage_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.Cardknox.Fields.UseShippingAddressAsBilling")]
        public bool UseShippingAddressAsBilling { get; set; }

        public bool UseShippingAddressAsBilling_OverrideForStore { get; set; }
    }
}