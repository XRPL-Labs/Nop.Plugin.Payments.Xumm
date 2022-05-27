# Step 1: Register a Xumm Developer account

[Sign up](https://apps.xumm.dev/3) for a Xumm Developer account, if you don't already have one.

Once you have access to the Xumm Developer Dashboard, You will be able to create an application with the needed API Key and Secret.

The Application credentials can be found inside the Xumm Developer Dashboard > Settings > Application credentials section.

After that, please input `https://<your-store-url>/Plugins/Xumm/Webhook` as the Webhook URL in the Application details section.

# Step 2: Install the plugin on nopCommerce
The Xumm Pay plugin is not (yet) available in the nopCommerce Marketplace so you need to create a folder `Payments.Xumm` in the nopCommerce Plugins folder and copy the contents of `src`.

More details can be found in the [nopCommerce Documentation](https://docs.nopcommerce.com/en/getting-started/advanced-configuration/plugins-in-nopcommerce.html).

# Step 3: Setup Xumm Pay on nopCommerce
After installing the Xumm Pay plugin, you can click Configuration > Payment methods on the navigation menu of the admin panel.

You will be able to see “Xumm” on the list, click Configure.

## API Settings

### API Credentials {#API-Credentials}
First you need to configure the API Key and API Secret found in the Xumm Application credentials section at the Developer Dashboard. 
The application will restart to apply the API Credentials since these are set at startup and will be validated after the page has automatically refreshed.

Other settings will be visible if valid credentials are configured.

### Webhook URL {#Webhook-URL}
The configured Webhook URL in the Xumm Developer Console will be matched with the necessary Webhook URL of the shop.
Xumm Pay will be hidden for consumers if the Webhook URL doesn't match the stated Webhook URL.

## XRPL Settings

### XRPL Address {#XRPL-Address}
Using the Sign in with Xumm fuctionality to set the XRPL Address is recommended to prevent a wrongly configured XRPL Address.

The [XRPL Currency](#XRPL-Currency) will be set to XRP if there was no XRPL Address previously set or no trust line of the configured [XRPL Currency](#XRPL-Currency) was set.

### XRPL Currency {#XRPL-Currency}
The XRPL Currency can be configured if the [XRPL Address](#XRPL-Address) is valid and the list is populated with Xumm's Curated Assets, XRPL Address Trust Lines and XRP.

You will be redirected the the required TrustSet flow if a curated asset has been selected but no trust line has been set on the [XRPL Address](#XRPL-Address).
The selected XRPL Currency will only be stored if the Trust Line is set during this flow.

#### Shop Currency
A Shop Currency with a Currency Code equal to the XRPL Currency Code has to exist and set as the Primary Store Currency.
Without this requirement the Xumm Pay option will not be visible to consumers.

#### Missing Trust Line
Xumm Pay will be hidden for consumers if the trust line has been removed later on.
For the ease of use a button will be shown next to the drop down list to redirect to the TrustSet flow.

### XRPL Destination Tag
An optional [destination tag](https://xrpl.org/source-and-destination-tags.html) can be set and is formatted as a 32-bit unsigned integer.

# Step 4: Validate Xumm Pay Configuration
The [API Key](#API-Credentials), [API Secret](#API-Credentials), [Webhook URL](#Webhook-URL), [XRPL Address](#XRPL-Address) and [XRPL Currency](#XRPL-Currency) has to be configured as required and show green checkmarks in the input fields. 

Xumm Pay will not be visible for consumers if any of those fields show a red exclamation mark.
