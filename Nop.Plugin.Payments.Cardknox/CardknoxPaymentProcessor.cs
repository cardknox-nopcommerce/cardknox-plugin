using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Cardknox.Models;
using Nop.Plugin.Payments.Cardknox.Validators;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using CardknoxSDK = Cardknox.Lib;

namespace Nop.Plugin.Payments.Cardknox
{
    /// <summary>
    /// Cardknox payment processor
    /// </summary>
    public class CardknoxPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CardknoxPaymentSettings _cardknoxPaymentSettings;
        private readonly CurrencySettings _currencySettings;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly IEncryptionService _encryptionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;

        #endregion Fields

        #region Ctor

        public CardknoxPaymentProcessor(CardknoxPaymentSettings cardknoxPaymentSettings,
            CurrencySettings currencySettings,
            ICurrencyService currencyService,
            ICustomerService customerService,
            IEncryptionService encryptionService,
            ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentService paymentService,
            ISettingService settingService,
            IWebHelper webHelper,
            IOrderTotalCalculationService orderTotalCalculationService)
        {
            this._cardknoxPaymentSettings = cardknoxPaymentSettings;
            this._currencySettings = currencySettings;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._encryptionService = encryptionService;
            this._localizationService = localizationService;
            this._logger = logger;
            this._orderProcessingService = orderProcessingService;
            this._orderService = orderService;
            this._paymentService = paymentService;
            this._settingService = settingService;
            this._webHelper = webHelper;
            _orderTotalCalculationService = orderTotalCalculationService;
        }

        #endregion Ctor

        #region Utilities

        private CardknoxSDK.CardknoxFacade PrepareCardknoxFacade()
        {
            CardknoxSDK.Credentials cardknoxCredentials = null;
            if (_cardknoxPaymentSettings.OverrideApiVersion)
            {
                cardknoxCredentials = new CardknoxSDK.Credentials(
                    _cardknoxPaymentSettings.TransactionKey,
                    _cardknoxPaymentSettings.SoftwareName,
                    _cardknoxPaymentSettings.SoftwareVersion,
                    _cardknoxPaymentSettings.ApiVersion);
            }
            else
            {
                cardknoxCredentials = new CardknoxSDK.Credentials(
                    _cardknoxPaymentSettings.TransactionKey,
                    _cardknoxPaymentSettings.SoftwareName,
                    _cardknoxPaymentSettings.SoftwareVersion);
            }

            return new CardknoxSDK.CardknoxFacade(cardknoxCredentials);
        }

        private void MapAddressToCardknox(Address address, CardknoxSDK.Actions.Common.PaymentRequest.Address cardknoxAddress)
        {
            cardknoxAddress.City = address.City;
            cardknoxAddress.Company = address.Company;
            cardknoxAddress.Country = address.Country.ThreeLetterIsoCode;
            cardknoxAddress.FirstName = address.FirstName;
            cardknoxAddress.LastName = address.LastName;
            cardknoxAddress.MiddleName = "";
            cardknoxAddress.MobilePhone = "";
            cardknoxAddress.Phone = address.PhoneNumber;
            cardknoxAddress.State = address.StateProvince.Abbreviation;
            cardknoxAddress.Street1 = address.Address1;
            cardknoxAddress.Street2 = address.Address2;
            cardknoxAddress.Zip = address.ZipPostalCode;
        }

        #endregion Utilities

        #region Methods

        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);

            var cardknoxFacade = PrepareCardknoxFacade();

            CardknoxSDK.Actions.Common.PaymentRequest cardknoxPaymentRequest = null;
            if (_cardknoxPaymentSettings.TransactMode == TransactMode.Authorize)
            {
                cardknoxPaymentRequest = new CardknoxSDK.Actions.AuthOnly.Request();
            }
            else
            {
                cardknoxPaymentRequest = new CardknoxSDK.Actions.Sale.Request();
            }

            //Fill info
            if (processPaymentRequest.CreditCardExpireYear > 99)
            {
                //Take only 2 digits
                var date2digits = new DateTime(processPaymentRequest.CreditCardExpireYear, 1, 1).ToString("yy");
                cardknoxPaymentRequest.ExpirationYear = Convert.ToInt32(date2digits);
            }
            else
            {
                cardknoxPaymentRequest.ExpirationYear = processPaymentRequest.CreditCardExpireYear;
            }

            cardknoxPaymentRequest.Name = processPaymentRequest.CreditCardName;
            cardknoxPaymentRequest.CardNumber = processPaymentRequest.CreditCardNumber;
            cardknoxPaymentRequest.ExpirationMonth = processPaymentRequest.CreditCardExpireMonth;
            cardknoxPaymentRequest.CVV = processPaymentRequest.CreditCardCvv2;
            cardknoxPaymentRequest.Amount = Math.Round(processPaymentRequest.OrderTotal, 2);

            cardknoxPaymentRequest.Email = customer.BillingAddress.Email;
            cardknoxPaymentRequest.CustomerIpAddress = _webHelper.GetCurrentIpAddress();
            cardknoxPaymentRequest.Street = customer.BillingAddress.Address1;
            cardknoxPaymentRequest.Zip = customer.BillingAddress.ZipPostalCode;

            cardknoxPaymentRequest.SendReceiptToCustomerEmail = _cardknoxPaymentSettings.SendReceiptToCustomerEmail;

            cardknoxPaymentRequest.Invoice = processPaymentRequest.OrderGuid.ToString();

            if (!_cardknoxPaymentSettings.HideAddressDetails)
            {
                cardknoxPaymentRequest.BillingAddress = new CardknoxSDK.Actions.Common.PaymentRequest.Address();
                cardknoxPaymentRequest.ShippingAddress = new CardknoxSDK.Actions.Common.PaymentRequest.Address();

                if (_cardknoxPaymentSettings.UseShippingAddressAsBilling)
                {
                    MapAddressToCardknox(customer.ShippingAddress, cardknoxPaymentRequest.BillingAddress);
                }
                else
                {
                    MapAddressToCardknox(customer.BillingAddress, cardknoxPaymentRequest.BillingAddress);
                }

                MapAddressToCardknox(customer.ShippingAddress, cardknoxPaymentRequest.ShippingAddress);
            }

            CardknoxSDK.Infra.IResponse response = null;
            if (_cardknoxPaymentSettings.TransactMode == TransactMode.Authorize)
            {
                response = cardknoxFacade.AuthOnly((CardknoxSDK.Actions.AuthOnly.Request)cardknoxPaymentRequest)
                    .GetAwaiter().GetResult();
            }
            else
            {
                response = cardknoxFacade.Sale((CardknoxSDK.Actions.Sale.Request)cardknoxPaymentRequest)
                    .GetAwaiter().GetResult();
            }

            if (response == null)
                return result;

            switch (response.ResponseType)
            {
                case CardknoxSDK.Infra.ResponseTypes.Accepted:
                    if (_cardknoxPaymentSettings.TransactMode == TransactMode.Authorize)
                    {
                        result.AuthorizationTransactionId = response.RefNum;
                        result.AuthorizationTransactionCode = response.RefNum;

                        result.NewPaymentStatus = PaymentStatus.Authorized;
                    }
                    else
                    {
                        result.CaptureTransactionId = response.RefNum;

                        result.NewPaymentStatus = PaymentStatus.Paid;
                    }

                    result.AuthorizationTransactionResult =
                        $"Payment request approved";
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Declined:
                    result.AddError($"Payment declined. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Error:
                    result.AddError($"Payment error. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Timeout:
                    result.AddError($"Payment timeout. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.HttpException:
                    result.AddError($"Communication error. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;
            }

            return result;
        }

        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _cardknoxPaymentSettings.AdditionalFee, _cardknoxPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            return false;
        }

        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();

            var cardknoxFacade = PrepareCardknoxFacade();

            var cardknoxRequest = new CardknoxSDK.Actions.Capture.Request()
            {
                RefNum = capturePaymentRequest.Order.AuthorizationTransactionId
            };

            var response = cardknoxFacade.Capture(cardknoxRequest)
                .GetAwaiter().GetResult();

            //validate
            if (response == null)
                return result;

            switch (response.ResponseType)
            {
                case CardknoxSDK.Infra.ResponseTypes.Accepted:
                    result.CaptureTransactionId = response.RefNum;

                    result.NewPaymentStatus = PaymentStatus.Paid;

                    result.CaptureTransactionResult =
                        $"Payment capture successful";
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Declined:
                    result.AddError($"Payment capture declined. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Error:
                    result.AddError($"Payment capture error. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Timeout:
                    result.AddError($"Payment capture timeout. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.HttpException:
                    result.AddError($"Communication error. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;
            }

            return result;
        }

        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();

            var cardknoxFacade = PrepareCardknoxFacade();

            var cardknoxRequest = new CardknoxSDK.Actions.Refund.Request()
            {
                RefNum = refundPaymentRequest.Order.CaptureTransactionId,
                Amount = refundPaymentRequest.AmountToRefund
            };

            var response = cardknoxFacade.Refund(cardknoxRequest)
                .GetAwaiter().GetResult();

            //validate
            if (response == null)
                return result;

            switch (response.ResponseType)
            {
                case CardknoxSDK.Infra.ResponseTypes.Accepted:
                    if (refundPaymentRequest.IsPartialRefund)
                    {
                        result.NewPaymentStatus = PaymentStatus.PartiallyRefunded;
                    }
                    else
                    {
                        result.NewPaymentStatus = PaymentStatus.Refunded;
                    }
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Declined:
                    result.AddError($"Payment refund declined. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Error:
                    result.AddError($"Payment refund error. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Timeout:
                    result.AddError($"Payment refund timeout. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.HttpException:
                    result.AddError($"Communication error. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;
            }

            return result;
        }

        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();

            var cardknoxFacade = PrepareCardknoxFacade();

            var cardknoxRequest = new CardknoxSDK.Actions.Void.Request()
            {
                RefNum = voidPaymentRequest.Order.AuthorizationTransactionId
            };

            var response = cardknoxFacade.Void(cardknoxRequest)
                .GetAwaiter().GetResult();

            //validate
            if (response == null)
                return result;

            switch (response.ResponseType)
            {
                case CardknoxSDK.Infra.ResponseTypes.Accepted:
                    result.NewPaymentStatus = PaymentStatus.Voided;
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Declined:
                    result.AddError($"Payment void declined. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Error:
                    result.AddError($"Payment void error. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.Timeout:
                    result.AddError($"Payment void timeout. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;

                case CardknoxSDK.Infra.ResponseTypes.HttpException:
                    result.AddError($"Communication error. Please try again. Error code: {response.ErrorCode} - Error Message: {response.ErrorMessage}");
                    break;
            }

            return result;
        }

        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            return result;
        }

        public void ProcessRecurringPayment(string transactionId)
        {
            //Nothing
        }

        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            return result;
        }

        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            return false;
        }

        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"]
            };

            var validationResult = validator.Validate(model);

            if (!validationResult.IsValid)
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest
            {
                CreditCardName = form["CardholderName"],
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };

            return paymentInfo;
        }

        public string GetPublicViewComponentName()
        {
            return "Cardknox";
        }

        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentCardknox/Configure";
        }

        public override void Install()
        {
            //settings
            var settings = new CardknoxPaymentSettings
            {
                TransactMode = TransactMode.Authorize,
                TransactionKey = "",
                HideAddressDetails = false,
                SendReceiptToCustomerEmail = false,
                SoftwareName = "nopCommerce Cardknox Plugin",
                SoftwareVersion = "Default",
                UseShippingAddressAsBilling = false
            };
            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseShippingAddressAsBilling", "Use shipping address.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseShippingAddressAsBilling.Hint", "Check if you want to use the shipping address as a billing address.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactModeValues", "Transaction mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactModeValues.Hint", "Choose transaction mode.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactionKey", "Transaction key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactionKey.Hint", "Specify transaction key.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareName", "Software Name to send to the Cardknox SDK (required)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareName.Hint", "This is a required field to declare to the SDK");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareVersion", "Software Version to send to the Cardknox SDK (required)");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareVersion.Hint", "This is a required field to declare to the SDK");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareApiVersion", "Custom API version to be used");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareApiVersion.Hint", "Leave this option empty to use the default API version");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseCustomSoftwareApiVersion", "Use a custom API version");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseCustomSoftwareApiVersion.Hint", "Leave this option unchecked to use the default API version");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Cardknox.PaymentMethodDescription", "Pay by credit card");

            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<CardknoxPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseShippingAddressAsBilling");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseShippingAddressAsBilling.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactModeValues");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactModeValues.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactionKey");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.TransactionKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareName");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareName.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareVersion");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.SoftwareVersion.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseCustomSoftwareApiVersion");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.Fields.UseCustomSoftwareApiVersion.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Cardknox.PaymentMethodDescription");

            base.Uninstall();
        }

        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "Cardknox";
        }

        #endregion Methods

        #region Properties

        public bool SupportCapture
        {
            get
            {
                return true;
            }
        }

        public bool SupportPartiallyRefund
        {
            get
            {
                return true;
            }
        }

        public bool SupportRefund
        {
            get
            {
                return true;
            }
        }

        public bool SupportVoid
        {
            get
            {
                return true;
            }
        }

        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        public string PaymentMethodDescription
        {
            get { return _localizationService.GetResource("Plugins.Payments.Cardknox.PaymentMethodDescription"); }
        }

        #endregion Properties
    }
}