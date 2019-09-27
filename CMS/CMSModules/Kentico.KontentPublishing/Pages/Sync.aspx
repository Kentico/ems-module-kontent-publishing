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
    <asp:PlaceHolder runat="server" ID="plcTextBox">
        <cms:LocalizedButton ID="btnSyncAll" runat="server" OnClick="btnSyncAll_Click" EnableViewState="false" Text="Synchronize all" ButtonStyle="Primary" />
        <cms:LocalizedButton runat="server" ID="btnDangerZone" OnClick="btnDangerZone_Click" Text="Show advanced actions" ButtonStyle="Default" />
        <asp:PlaceHolder runat="server" ID="plcDangerZone" Visible="false">
            <cms:LocalizedButton ID="btnSyncMediaLibraries" runat="server" OnClick="btnSyncMediaLibraries_Click" EnableViewState="false" Text="Synchronize media libraries" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnSyncRelationships" runat="server" OnClick="btnSyncRelationships_Click" EnableViewState="false" Text="Synchronize relationships" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnSyncCategories" runat="server" OnClick="btnSyncCategories_Click" EnableViewState="false" Text="Synchronize categories" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnSyncContentTypes" runat="server" OnClick="btnSyncContentTypes_Click" EnableViewState="false" Text="Synchronize content types" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnSyncAttachments" runat="server" OnClick="btnSyncAttachments_Click" EnableViewState="false" Text="Synchronize attachments" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnSyncLanguages" runat="server" OnClick="btnSyncLanguages_Click" EnableViewState="false" Text="Synchronize cultures" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnSyncPages" runat="server" OnClick="btnSyncPages_Click" EnableViewState="false" Text="Synchronize pages" ButtonStyle="Default" />
            <cms:LocalizedButton ID="btnDeleteAll" runat="server" OnClick="btnDeleteAll_Click" EnableViewState="false" Text="Delete all data in Kentico Cloud" ButtonStyle="Default" OnClientClick="return confirm('This will delete all data in the target Kentico Cloud project, not only the data synchronized from this site. Are you sure you want to continue?')" />
        </asp:PlaceHolder>
    </asp:PlaceHolder>
</asp:Content>
