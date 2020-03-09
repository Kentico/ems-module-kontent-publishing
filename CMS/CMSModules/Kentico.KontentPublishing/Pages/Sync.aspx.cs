using System;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using CMS.DataEngine;
using CMS.UIControls;

using Kentico.EMS.Kontent.Publishing;

[UIElement("Kentico.KontentPublishing", "KontentPublishing")]
public partial class CMSModules_Kentico_KontentPublishing_Pages_Sync : GlobalAdminPage
{
    static CancellationTokenSource _cancellation = new CancellationTokenSource();

    /// <summary>
    /// Current Error.
    /// </summary>
    private string CurrentError
    {
        get
        {
            return ctlAsyncLog.ProcessData.Error;
        }
        set
        {
            ctlAsyncLog.ProcessData.Error = value;
        }
    }

    protected void Page_Load(object sender, EventArgs e)
    {
        PageTitle.TitleText = "Kentico Kontent Publishing";

        PageBreadcrumbs.Items.Add(new BreadcrumbItem
        {
            Text = "Kentico Kontent Publishing",
        });

        if (!GetModule().isConfigurationValid())
        {
            plcSync.Visible = false;
            ShowWarning("Kentico Kontent Publishing configuration is invalid, check the configuration.");
            return;
        }

        ctlAsyncLog.TitleText = "Synchronization in progress ...";

        ctlAsyncLog.OnFinished += OnFinished;
        ctlAsyncLog.OnCancel += OnCancel;
        ctlAsyncLog.OnError += OnError;
        ctlAsyncLog.OnRequestLog += OnRequestLog;
    }

    private void OnRequestLog(object sender, EventArgs e)
    {
        ctlAsyncLog.LogContext = SyncLog.CurrentLog;
    }

    private void OnError(object sender, EventArgs e)
    {
        if (ctlAsyncLog.Status == AsyncWorkerStatusEnum.Running)
        {
            ctlAsyncLog.Stop();
        }
        ShowError(CurrentError);
    }

    private void OnFinished(object sender, EventArgs e)
    {
        pnlLog.Visible = false;
        if (!string.IsNullOrEmpty(CurrentError))
        {
            ShowError(CurrentError);
        }
        else
        {
            ShowInformation("Synchronization finished");
        }
    }


    private void OnCancel(object sender, EventArgs e)
    {
        Cancel();
        pnlLog.Visible = false;
        ShowWarning("Synchronization cancelled");
    }

    private void Cancel()
    {
        _cancellation.Cancel();
        _cancellation = new CancellationTokenSource();
    }

    private void RunSync(Func<CancellationToken, Task> sync)
    {
        // Cancel anything that might previously run
        Cancel();

        pnlLog.Visible = true;

        CurrentError = string.Empty;

        btnSyncAll.Enabled = false;
        btnSyncContentTypes.Enabled = false;
        btnSyncPages.Enabled = false;
        btnSyncMediaLibraries.Enabled = false;

        SyncLog.CurrentLog = ctlAsyncLog.EnsureLog();

        var cancellation = _cancellation;

        ctlAsyncLog.RunAsync(
            parameter => Task.Run(async () =>
            {
                try
                {
                    await sync(cancellation.Token);
                }
                catch (Exception ex)
                {
                    SyncLog.LogException("KenticoKontentPublishing", "UNHANDLEDERROR", ex);
                    SyncLog.Log(ex.Message);
                    ctlAsyncLog.ProcessData.Error = ex.Message;
                }
            }).Wait(),
            WindowsIdentity.GetCurrent()
        );
    }

    private KontentPublishingModule GetModule()
    {
        return ModuleManager.GetModule(KontentPublishingModule.MODULE_NAME) as KontentPublishingModule;
    }

    private async Task SyncRelationships(CancellationToken cancellation)
    {
        await GetModule()?.SyncRelationships(cancellation);
    }

    private async Task SyncCategories(CancellationToken cancellation)
    {
        await GetModule()?.SyncCategories(cancellation);
    }

    private async Task SyncMediaFolders(CancellationToken cancellation)
    {
        await GetModule()?.SyncMediaFolders();
    }

    private async Task SyncMediaLibraries(CancellationToken cancellation)
    {
        await GetModule()?.SyncMediaLibraries(cancellation);
    }

    private async Task SyncContentTypes(CancellationToken cancellation)
    {
        await GetModule()?.SyncContentTypes(cancellation);
    }

    private async Task SyncAttachments(CancellationToken cancellation)
    {
        await GetModule()?.SyncAttachments(cancellation);
    }

    private async Task SyncPages(CancellationToken cancellation)
    {
        await GetModule()?.SyncPages(cancellation);
    }

    private async Task SyncLanguages(CancellationToken cancellation)
    {
        await GetModule()?.SyncLanguages(cancellation);
    }

    private async Task DeleteAll(CancellationToken cancellation)
    {
        await GetModule()?.DeleteAll(cancellation);
    }

    private async Task SyncAll(CancellationToken cancellation)
    {
        await GetModule()?.SyncAll(cancellation);
    }

    protected void btnSyncAll_Click(object sender, EventArgs e)
    {
        RunSync(SyncAll);
    }

    protected void btnSyncMediaFolders_Click(object sender, EventArgs e)
    {
        RunSync(SyncMediaFolders);
    }

    protected void btnSyncMediaLibraries_Click(object sender, EventArgs e)
    {
        RunSync(SyncMediaLibraries);
    }

    protected void btnSyncContentTypes_Click(object sender, EventArgs e)
    {
        RunSync(SyncContentTypes);
    }

    protected void btnSyncAttachments_Click(object sender, EventArgs e)
    {
        RunSync(SyncAttachments);
    }

    protected void btnSyncLanguages_Click(object sender, EventArgs e)
    {
        RunSync(SyncLanguages);
    }

    protected void btnSyncPages_Click(object sender, EventArgs e)
    {
        RunSync(SyncPages);
    }

    protected void btnSyncRelationships_Click(object sender, EventArgs e)
    {
        RunSync(SyncRelationships);
    }

    protected void btnSyncCategories_Click(object sender, EventArgs e)
    {
        RunSync(SyncCategories);
    }

    protected void btnDeleteAll_Click(object sender, EventArgs e)
    {
        RunSync(DeleteAll);
    }

    protected void btnDangerZone_Click(object sender, EventArgs e)
    {
        pnlDangerZone.Visible = true;
        btnDangerZone.Visible = false;
        ShowWarning("DANGER ZONE: The extra actions are meant for debug and cleanup purposes, use with caution.");
    }
}