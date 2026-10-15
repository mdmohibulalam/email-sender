namespace CampaignStudio.Api;
public sealed class SignupWorker(IServiceScopeFactory scopes,StudioSettings settings,ILogger<SignupWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try{using var scope=scopes.CreateScope();await SignupForms.ProcessOne(scope.ServiceProvider.GetRequiredService<Store>(),settings,scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds(),ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){logger.LogError("Confirmation worker failed ({ErrorType}).",ex.GetType().Name);}
            try{await Task.Delay(1000,ct);}catch(OperationCanceledException){break;}
        }
    }
}
public sealed class InboxWorker(IServiceScopeFactory scopes,ILogger<InboxWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try{using var scope=scopes.CreateScope();await Inboxes.ProcessOne(scope.ServiceProvider.GetRequiredService<Store>(),scope.ServiceProvider,scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds(),ct);}
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}catch(Exception ex){logger.LogError("Inbox worker failed ({ErrorType}).",ex.GetType().Name);}
            try{await Task.Delay(1000,ct);}catch(OperationCanceledException){break;}
        }
    }
}
