using System;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.Cardknox.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Cardknox.Controllers
{
	public class PaymentCardknoxController : BasePaymentController
	{
		private readonly ILocalizationService _localizationService;
		private readonly ISettingService _settingService;
		private readonly IStoreContext _storeContext;
		private readonly IPaymentService _paymentService;
		private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;

        public PaymentCardknoxController(ILocalizationService localizationService,
			ISettingService settingService,
			IStoreContext storeContext,
			IPaymentService paymentService,
			IPermissionService permissionService,
            INotificationService notificationService)
		{
			this._localizationService = localizationService;
			this._settingService = settingService;
			this._storeContext = storeContext;
			this._paymentService = paymentService;
			this._permissionService = permissionService;
            _notificationService = notificationService;
        }

		[AuthorizeAdmin]
		[Area(AreaNames.Admin)]
		public IActionResult Configure()
		{
			if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
				return AccessDeniedView();

			//load settings for a chosen store scope
			var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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
		[AuthorizeAdmin]
		[Area(AreaNames.Admin)]
		public IActionResult Configure(ConfigurationModel model)
		{
			if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
				return AccessDeniedView();

			if (!ModelState.IsValid)
				return Configure();

			//load settings for a chosen store scope
			var storeScope = _storeContext.ActiveStoreScopeConfiguration;
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

			_notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

			return Configure();
		}
	}
}