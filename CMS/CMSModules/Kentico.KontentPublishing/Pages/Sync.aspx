<%@ Page Language="C#" AutoEventWireup="true" Inherits="CMSModules_Kentico_KontentPublishing_Pages_Sync"
    Theme="Default" MasterPageFile="~/CMSMasterPages/UI/SimplePage.master" Title="Kentico Cloud Publishing" CodeBehind="Sync.aspx.cs" %>

<%@ Register Src="~/CMSAdminControls/AsyncLogDialog.ascx" TagName="AsyncLog"
    TagPrefix="cms" %>

<asp:Content ContentPlaceHolderID="plcBeforeBody" runat="server" ID="cntBeforeBody">
    <asp:Panel runat="server" ID="pnlLog" Visible="false">
        <cms:AsyncLog ID="ctlAsyncLog" runat="server" ProvideLogContext="true" />
    </asp:Panel>
</asp:Content>
<asp:Content ContentPlaceHolderID="plcContent" runat="server">
    <asp:PlaceHolder runat="server" ID="plcSync">
        <cms:LocalizedButton ID="btnSyncAll" runat="server" OnClick="btnSyncAll_Click" EnableViewState="false" Text="Synchronize all" ButtonStyle="Primary" />
        <cms:LocalizedButton runat="server" ID="btnDangerZone" OnClick="btnDangerZone_Click" Text="Show advanced actions" ButtonStyle="Default" />
        <asp:Panel runat="server" ID="pnlDangerZone" Visible="false">
            <br />
            <cms:LocalizedButton ID="btnDeleteAll" runat="server" OnClick="btnDeleteAll_Click" EnableViewState="false" Text="Delete all data in Kentico Kontent" ButtonStyle="Default" OnClientClick="return confirm('This will delete all data in the target Kentico Cloud project, not only the data synchronized from this site. Are you sure you want to continue?')" />
            <br /><br />
            <h4>Granular sync to Kontent</h4>
            <p>Execute in the defined order after deleting all. Some stages can be re-synced individually unless there is a strong reference from later object types.</p>
            <cms:LocalizedButton ID="btnSyncMediaLibraries" runat="server" OnClick="btnSyncMediaLibraries_Click" EnableViewState="false" Text="Synchronize media libraries" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnSyncRelationships" runat="server" OnClick="btnSyncRelationships_Click" EnableViewState="false" Text="Synchronize relationships" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnSyncCategories" runat="server" OnClick="btnSyncCategories_Click" EnableViewState="false" Text="Synchronize categories" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnSyncContentTypes" runat="server" OnClick="btnSyncContentTypes_Click" EnableViewState="false" Text="Synchronize content types" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnSyncAttachments" runat="server" OnClick="btnSyncAttachments_Click" EnableViewState="false" Text="Synchronize attachments" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnSyncLanguages" runat="server" OnClick="btnSyncLanguages_Click" EnableViewState="false" Text="Synchronize cultures" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnSyncPages" runat="server" OnClick="btnSyncPages_Click" EnableViewState="false" Text="Synchronize pages" ButtonStyle="Default" />
            <br /><br />
            <h4>Granular cleanup in Kontent</h4>
            <p>Execute in the defined order, some actions may not succeed when previous data was not deleted due to references to previous object types.</p>
            <cms:LocalizedButton ID="btnDeleteItems" runat="server" OnClick="btnDeleteItems_Click" EnableViewState="false" Text="Delete content items" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnDeleteContentTypes" runat="server" OnClick="btnDeleteContentTypes_Click" EnableViewState="false" Text="Delete content types" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnDeleteSnippets" runat="server" OnClick="btnDeleteSnippets_Click" EnableViewState="false" Text="Delete content type snippets" ButtonStyle="Default" />
            <br /><br />
            <cms:LocalizedButton ID="btnDeleteAssets" runat="server" OnClick="btnDeleteAssets_Click" EnableViewState="false" Text="Delete assets" ButtonStyle="Default" />
            <br /><br />
        </asp:Panel>
    </asp:PlaceHolder>
</asp:Content>
