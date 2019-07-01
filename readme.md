# Publishing content from Kentico EMS to Kentico Cloud

This repository contains source code of the Kentico Cloud publishing module for Kentico EMS.

The module automatically synchronizes all published content from the specific site in [Kentico EMS](https://www.kentico.com) to specific project in [Kentico Cloud](https://www.kenticocloud.com) using Kentico Cloud Content Management API.

The synchronization is only in direction from Kentico EMS to Kentico Cloud, not vice-versa.

## How to use this repository

### Manual installation with the source code from repository

Create a new empty project in Kentico Cloud.

Copy the contents of this repository to the root of your Kentico EMS installation.

Open the Kentico EMS solution in Visual Studio, and add project **KenticoCloudPublishing** to the solution.

Add reference to project KenticoCloudPublishing to the CMSApp project.

Update Kentico NuGet packages for the KenticoCloudPublishing project to the same version as your current hotfix version of Kentico EMS.

Build the solution.

Add the following keys to the web.config (or app.config) of your project(s)

```
	<add key="KCSyncSitename" value="<SITE CODE NAME>" />
    <add key="KCSyncWebRoot" value="<URL OF THE TARGET WEB SITE>" />
    <add key="KCSyncAssetsDomain" value="<KENTICO CLOUD ASSET DOMAIN>" />
    <add key="KCSyncProjectID" value="<YOUR PROJECT ID>" />
    <add key="KCSyncCMAPIKey" value="<YOUR CM API KEY>" />
```

`KCSyncSitename` is the code name of the site you want to synchronize to Kentico Cloud, e.g. `DancingGoatMvc`
`KCSyncWebRoot` is the root URL of the target site, to which relative URLs will be resolved, e.g. `https://www.dancinggoat.com`
`KCSyncAssetsDomain` is the domain name on which the assets in your Kentico Cloud project will be located. It depends on the geographical location in which your Kentico Cloud project is hosted. e.g. `assets-us-01.kc-usercontent.com`
`KCSyncProjectID` and `KCSyncCMAPIKey` can be found in the **API Keys** section of the **Project settings** of your target Kentico Cloud Project.

Go to **Kentico EMS administration -> Sites -> Import Site or objects** and Import package **Kentico.KenticoCloudPublishing_1.0.0.zip**.

Refresh the administration.

### Synchronize content to Kentico Cloud

Navigate to application **Kentico Cloud Publishing**.

Click **Synchronize all** to copy all currently published content to Kentico Cloud.

New changes to the published content will synchronize automatically.

### Customization

Edit the synchronization code in **KenticoCloudPublishing** project.

Click **Synchronize all** to update all currently published content in Kentico Cloud.

![Analytics](https://kentico-ga-beacon.azurewebsites.net/api/UA-69014260-4/Kentico/kentico-cloud-publishing?pixel)
