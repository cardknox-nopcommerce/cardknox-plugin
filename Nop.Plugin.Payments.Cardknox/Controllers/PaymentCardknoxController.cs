using Nop.Core;
using Nop.Plugin.Payments.Cardknox.Models;
using Nop.Plugin.Payments.Cardknox.Validators;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace Nop.Plugin.Payments.Cardknox.Controllers
{
    public class PaymentCardknoxController : BasePaymentController
    {
        private readonly ILocalizationService _localizationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IPaymentService _paymentService;
        private readonly IPermissionService _permissionService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;

        public PaymentCardknoxController(ILocalizationService localizationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IPaymentService paymentService,
            IPermissionService permissionService,
            IStoreService storeService,
            IWorkContext workContext)
        {
            this._localizationService = localizationService;
            this._settingService = settingService;
            this._storeContext = storeContext;
            this._paymentService = paymentService;
            this._permissionService = permissionService;
            _storeService = storeService;
            _workContext = workContext;
        }

        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure()
        {
            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var cardknoxPaymentSettings = _settingService.LoadSetting<CardknoxPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseShippingAddressAsBilling = cardknoxPaymentSettings.UseShippingAddressAsBilling,
                TransactModeId = Convert.ToInt32(cardknoxPaymentSettings.TransactMode),
                TransactionKey = cardknoxPaymentSettings.TransactionKey,
                AdditionalFee = cardknoxPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = cardknoxPaymentSettings.AdditionalFeePercentage,
                TransactModeValues = cardknoxPaymentSettings.TransactMode.ToSelectList(),
                ActiveStoreScopeConfiguration = storeScope,
                SoftwareApiVersion = cardknoxPaymentSettings.ApiVersion,
                UseCustomSoftwareApiVersion = cardknoxPaymentSettings.OverrideApiVersion,
                SoftwareVersion = cardknoxPaymentSettings.SoftwareVersion,
                SoftwareName = cardknoxPaymentSettings.SoftwareName
            };

            model.ActiveStoreScopeConfiguration = storeScope;
            if (storeScope > 0)
            {
                model.UseShippingAddressAsBilling_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.UseShippingAddressAsBilling, storeScope);
                model.TransactModeId_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.TransactMode, storeScope);
                model.TransactionKey_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.TransactionKey, storeScope);
                model.AdditionalFee_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
                model.SoftwareApiVersion_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.ApiVersion, storeScope);
                model.UseCustomSoftwareApiVersion_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.OverrideApiVersion, storeScope);
                model.SoftwareVersion_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.SoftwareVersion, storeScope);
                model.SoftwareName_OverrideForStore = _settingService.SettingExists(cardknoxPaymentSettings, x => x.SoftwareName, storeScope);
            }

            return View("~/Plugins/Payments.Cardknox/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AdminAuthorize]
        [ChildActionOnly]
        public ActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var cardknoxPaymentSettings = _settingService.LoadSetting<CardknoxPaymentSettings>(storeScope);

            //save settings
            cardknoxPaymentSettings.UseShippingAddressAsBilling = model.UseShippingAddressAsBilling;
            cardknoxPaymentSettings.TransactMode = (TransactMode)model.TransactModeId;
            cardknoxPaymentSettings.TransactionKey = model.TransactionKey;
            cardknoxPaymentSettings.AdditionalFee = model.AdditionalFee;
            cardknoxPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;
            cardknoxPaymentSettings.ApiVersion = model.SoftwareApiVersion;
            cardknoxPaymentSettings.OverrideApiVersion = model.UseCustomSoftwareApiVersion;
            cardknoxPaymentSettings.SoftwareVersion = model.SoftwareVersion;
            cardknoxPaymentSettings.SoftwareName = model.SoftwareName;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.UseShippingAddressAsBilling, model.UseShippingAddressAsBilling_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.TransactMode, model.TransactModeId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.TransactionKey, model.TransactionKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.ApiVersion, model.SoftwareApiVersion_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.OverrideApiVersion, model.UseCustomSoftwareApiVersion_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.SoftwareVersion, model.SoftwareVersion_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(cardknoxPaymentSettings, x => x.SoftwareName, model.SoftwareName_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [ChildActionOnly]
        public ActionResult PaymentInfo()
        {
            var model = new PaymentInfoModel();

            //years
            for (var i = 0; i < 15; i++)
            {
                var year = Convert.ToString(DateTime.Now.Year + i);
                model.ExpireYears.Add(new SelectListItem
                {
                    Text = year,
                    Value = year,
                });
            }

            //months
            for (var i = 1; i <= 12; i++)
            {
                var text = i < 10 ? "0" + i : i.ToString();
                model.ExpireMonths.Add(new SelectListItem
                {
                    Text = text,
                    Value = i.ToString(),
                });
            }

            //set postback values (we cannot access "Form" with "GET" requests)
            if (Request.HttpMethod == "GET")
                return View("~/Plugins/Payments.Cardknox/Views/PaymentInfo.cshtml", model);

            var form = Request.Form;
            model.CardholderName = form["CardholderName"];
            model.CardNumber = form["CardNumber"];
            model.CardCode = form["CardCode"];
            var selectedMonth = model.ExpireMonths.FirstOrDefault(x =>
                x.Value.Equals(form["ExpireMonth"], StringComparison.InvariantCultureIgnoreCase));

            if (selectedMonth != null)
                selectedMonth.Selected = true;

            var selectedYear = model.ExpireYears.FirstOrDefault(x =>
                x.Value.Equals(form["ExpireYear"], StringComparison.InvariantCultureIgnoreCase));

            if (selectedYear != null)
                selectedYear.Selected = true;

            return View("~/Plugins/Payments.Cardknox/Views/PaymentInfo.cshtml", model);
        }

        [NonAction]
        public override IList<string> ValidatePaymentForm(FormCollection form)
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

        [NonAction]
        public override ProcessPaymentRequest GetPaymentInfo(FormCollection form)
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
    }
}