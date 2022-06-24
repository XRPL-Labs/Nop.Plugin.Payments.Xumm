
# Install plugin
## Step 1: Register a Xumm Developer account

[Sign up](https://apps.xumm.dev/) for a Xumm Developer account, if you don't already have one.

Once you have access to the Xumm Developer Dashboard, you will be able to create an application with the required API Credentials.

The credentials can be found inside the Xumm Developer Dashboard > Settings > Application credentials section.

After that, please input `https://<your-store-url>/Plugins/Xumm/Webhook` as the Webhook URL in the Application details section.

## Step 2: Install the plugin in nopCommerce
The Xumm Pay plugin is not (yet) available in the nopCommerce Marketplace so you need to create a folder `Payments.Xumm` in the nopCommerce Plugins folder and copy the contents of `src`.

More details can be found in the [nopCommerce Documentation](https://docs.nopcommerce.com/en/getting-started/advanced-configuration/plugins-in-nopcommerce.html).

## Step 3: Configuration Xumm plugin
After installing the plugin, you can navigate to Configuration > Payment methods on the navigation menu of the admin panel.

You will be able to see “Xumm” on the list, click Configure.

### API Settings

#### API Credentials
First you need to configure the API Key and Secret found of the Application credentials section at the [Developer Dashboard](https://apps.xumm.dev/). 
The application will restart after save to apply the API Credentials since these are set at startup.

Other settings will be visible if the configured credentials are valid.

#### Webhook URL
Xumm uses webhooks to process payments and refunds and it's required to configure the URL in the [Developer Dashboard](https://apps.xumm.dev/).
The payment method will be hidden in the shoppingcart if the Webhook URL shown at the plugins configuration page is not configured.

### XRPL Settings

#### XRPL Address
Using the "Sign in with Xumm" button to set the XRPL Address is recommended to prevent a wrongly configured XRPL Address.

Changing the address could change the [XRPL Currency](#xrpl-currency) to XRP if the new account doesn't have a trust line set of the previously configured [XRPL Currency](#xrpl-currency),

#### XRPL Currency
The XRPL Currency can be configured if the [XRPL Address](#xrpl-address) is valid and the list is populated with Xumm's Curated Assets, Trust Lines of [XRPL Address](#xrpl-address) and XRP.

You will be redirected to the required TrustSet flow if a curated asset has been selected but no trust line has been set on the configured [XRPL Address](#xrpl-address).
The selected XRPL Currency will only be saved if the Trust Line is set during this flow.

#### Shop Currency
A Shop Currency equal to the[XRPL Currency](#xrpl-currency) has to exist and set as the Primary Store Currency.
An error message at the configuration page will be shown if the shop currency is missing and the Xumm payment method will be hidden in the shoppingcart.

#### Missing Trust Line
The Xumm payment method will be hidden for consumers if the trust line of [XRPL Currency](#xrpl-currency) has been removed from [XRPL Address](#xrpl-address).
For the ease of use a button will be shown next to the drop down list at the configuration page to redirect to the TrustSet flow.

#### XRPL Destination Tags
An optional [destination tag](https://xrpl.org/source-and-destination-tags.html) can be set separately for payments and refunds. A destination tag is formatted as a 32-bit unsigned integer.

## Step 4: Validate Xumm Pay Configuration
The [API Key](#api-credentials), [API Secret](#api-credentials), [Webhook URL](#webhook-url), [XRPL Address](#xrpl-address) and [XRPL Currency](#xrpl-currency) has to be configured as required. 

The Xumm payment method will not be visible for consumers if not configured correctly.

# Refunds
Refunds are made possible by using the mailing system of nopCommerce because transactions on the XRPL needs to be signed.
The refund email will be send to the logged in user that initiates the refund via the admin panel and will contain a link to the sign page of https://xumm.app/.

# Pay with any XRPL token
Paying with any XRPL token via Xumm will be made possible after the implementation and release of Xumm Feature [#392](https://github.com/XRPL-Labs/XUMM-Issue-Tracker/issues/392).