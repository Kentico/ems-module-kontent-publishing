# Publishing content from Kentico Xperience to Kentico Kontent

[![Stack Overflow](https://img.shields.io/badge/Stack%20Overflow-ASK%20NOW-FE7A16.svg?logo=stackoverflow&logoColor=white)](https://stackoverflow.com/tags/kentico)

This repository contains the source code of the *Kentico Xperience* - *Kentico Kontent* publishing module.

The module automatically synchronizes all published content (pages, page types) and assets (media files, page attachments) from a specific single site in [Kentico Xperience](https://xperience.io/) to a specific project in [Kentico Kontent](https://kontent.ai) using the Content Management API.

The module is designed to publish content only from *Kentico Xperience* to *Kentico Kontent*, not vice-versa.

The typical use case for the module is providing a reliable headless endpoint for your additional channels, e.g. a mobile application.

## Setting up the environment 

### Installing the Xperience module

To install the module to your Xperience instance:

1. Copy the contents of this repository to the root of your Xperience installation.

   * You can do that by cloning the repository to a local folder (other than your Xperience folder) and then copying the files over to your Xperience folder.

   * Only the files from the following folders are necessary for a proper installation of the module:  
     * `/Kentico.KontentPublishing`
     * `/CMS`

2. Open the Xperience solution in Visual Studio, and add the **Kentico.KontentPublishing** project to the solution.

   * Include all the content from the `/CMS/CMSModules/Kentico.KontentPublishing` folder to the CMSApp project.

   * Add a **reference** to the **Kentico.KontentPublishing** project in the CMSApp project.

     * OPTIONAL: In case you have more projects, e.g. an MVC site instance, add the same reference to those projects, as well.

3. **Update Kentico NuGet packages** for the the *Kentico.KontentPublishing* project to the same version as your current hotfix version of *Kentico Xperience*.

4. Build the solution.

### Connecting to Kontent

1. Create a **new empty project** in [Kentico Kontent](https://app.kontent.ai).

2. Add the following keys to the web.config (or app.config) of your project(s)

```
<add key="KCSyncSitename" value="[SITE CODE NAME]" />
<add key="KCSyncWebRoot" value="[URL OF THE TARGET WEB SITE]" />
<add key="KCSyncAssetsDomain" value="[KENTICO KONTENT ASSET DOMAIN]" />
<add key="KCSyncProjectID" value="[YOUR PROJECT ID]" />
<add key="KCSyncCMAPIKey" value="[YOUR CM API KEY]" />
```

  - `KCSyncSitename` is the code name of the site you want to synchronize with your *Kontent* project, e.g. `DancingGoatMvc`

  - `KCSyncWebRoot` is the root URL of the target site, to which relative URLs will be resolved, e.g. `https://www.dancinggoat.com`

  - `KCSyncAssetsDomain` is the domain name on which the assets in your *Kontent* project will be located. It depends on the geographical location in which your *Kontent* project is hosted, e.g. `assets-us-01.kc-usercontent.com`.

     - The easiest way to find out this domain name is to upload a new temporary asset (in *Kontent*) in the **Content & Assets**, and use the **Copy asset URL** action in the actions menu. You can then find the domain in the copy dialog.

     ![Copy asset URL](images/CopyAssetUrl.png)

     - After you get the domain name, you can delete the asset.

  - `KCSyncProjectID` and `KCSyncCMAPIKey` can be found in the **API Keys** section of the **Project settings** of your target *Kentico Kontent* Project.

### Importing the Xperience module

1. Navigate to the **Xperience administration -> Sites application -> Import Site or objects** 
2. Import the **Kentico.KontentPublishing_1.0.0.zip** package. If you properly copied the module files, the package should be automatically offered in this dialog.

  ![Import package](images/ImportPackage.png)

3. Refresh the administration interface, e.g. by reloading the page in your web browser.

The **Kentico Kontent Publishing** application should be now available in your application list.

## Working with the module

### Synchronizing content

![Module user interface](images/KenticoKontentPublishing.png)

To synchronize your content between *Kentico Xperience* and *Kentico Kontent*:

1. In the *Xperience* admnistration interface, open the **Kentico Kontent Publishing** application.

2. Click the **Synchronize all** button to copy all currently published content to *Kentico Kontent* and wait until the synchronization finishes.

   ![Synchronizing changes](images/KenticoKontentPublishingSync.png)

After the synchronization is finished, *Kentico Kontent* will contain all your published content from *Kentico Xperience*. Examine the content and update the content structure in *Xperience* if necessary.

   ![Published content](images/PublishedContent.png)

New changes to the published content in *Xperience* will synchronize automatically.

Once you perform the synchronization, you should edit your content only in *Kentico Xperience*. Any modifications done in *Kontent* might get overwritten by changes in *Xperience*. However, you can still observe and navigate data in *Kentico Kontent* while developing or debugging your target application.

### Migrating content

It is possible to transfer all the content and assets from *Kentico Xperience* to *Kentico Kontent* as plaintext HTML using this module.

In case you would like to embrace a fully headless CMS:
1. Set up the module and run full synchronization as described in the sections above. 
2. Remove the module from your *Xperience* instance. 
3. Continue with editing the content in your *Kontent* project. 

Please, note the following:
- The content is migrated as plaintext and any styles applied in *Xperience* (such as bold, or italics) are transferred to *Kontent* as HTML tags. Therefore, we recommend to use this approach of migrating content only in specific scenarios, e.g. when majority of your content is in plaintext form without any styles.
- The module synchronizes only published content. If you have any unpublished content that you wish to migrate, publish it before the data migration.


### Customizing the module

If you wish to customize this module, you can edit the synchronization part of the code in the **Kentico.KontentPublishing** project.

To perform partial updates while customizing the code or to purge all project data:
1. Click on the **Synchronize all** button in the **Kentico Kontent Publishing** application.
2. Click on the **Show advanced actions** button and select the action you wish to perform.

Please, note that it is not recommended to mix synchronized content from *Kentico Xperience* with manually created content in *Kentico Kontent*. "Mixing" content could result in  overwritten or lost data, because deleting the project data removes all the content from your project, no matter where it originated.

## Development

You can merge this repository with your existing Kentico Xperience project and make commits to it provided it doesn't have it's own git repository.

Follow the installation guide, but instead of cloning the repository to another folder and copying it over to your installation, clone it directly to your existing Kentico Xperience installation.

Run the following commands in command line:

```
cd your/kentico/xperience/root/folder
git init
echo * > .gitignore
git remote add origin https://github.com/Kentico/xperience-module-kontent-publishing.git
git fetch
git checkout origin/master -b master
```

After this, you can easily edit the code, and commit changes to the original repository. Feel free to submit any pull requests with fixed or enhanced functionality.

## Uninstalling the module

To remove the module from your *Xperience* instance:
1. Remove the *Kentico.KontentPublishing* project from your solution(s).
2. Remove all files introduced by this repository.
3. Delete the *Kentico Kontent Publishing* module from the Xperience administration interface.
4. Rebuild the solution(s).

![Analytics](https://kentico-ga-beacon.azurewebsites.net/api/UA-69014260-4/Kentico/ems-module-kontent-publishing?pixel)

## Remarks & Known issues

* Pages set to be published in the future (with the *PublishFrom* field correspondingly set) will be automatically synchronized to *Kontent* once they are published in *Xperience*. However, pages unpublished in the future (with the *PublishTo* field set) will stay in *Kontent* even after unpublished in *Xperience*.
* Only certain page fields and relationships are synchronized. E.g. SKU-related page fields are not synchronized.
* Files from *Xperience* media libraries are always synchronized into a "flat" structure in *Kontent*. I.e. the original folder structure of media library files is not preserved.
* The preview file of media library files is not synchronized.
* Only limited conversion of page type fields between *Xperience* and *Kontent* projects is supported.
* Workflows configured in *Xperience* might not work correctly when content will be manipulated with through the *Kontent* project.
* Synchronization of content is always performed either manually (via the *Xperience* application) or triggered by events. Regular content synchronization is not yet supported.
* The module is designed for single site projects only.

## [Questions & Support](https://github.com/Kentico/Home/blob/master/README.md)

See the [Kentico home repository](https://github.com/Kentico/Home/blob/master/README.md) for more information about the product(s), general advice on submitting your questions or directly contacting us.

![Analytics](https://kentico-ga-beacon.azurewebsites.net/api/UA-69014260-4/Kentico/ems-module-kontent-publishing?pixel)
