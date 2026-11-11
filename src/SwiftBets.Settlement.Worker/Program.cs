using SwiftBets.BuildingBlocks.Observability;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Settlement.Application;
using SwiftBets.Settlement.Infrastructure;
using SwiftBets.Settlement.Worker;
using SwiftBets.Settlement.Worker.Grpc;

if (HealthProbe.TryRun(args) is { } probeExitCode)
{
    return probeExitCode;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddSwiftBetsObservability("swiftbets-settlement");
builder.Services.AddSwiftBetsWeb();
builder.Services.AddSwiftBetsJwtBearer(builder.Configuration);
builder.Services.AddGrpc(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddSettlementApplication();
builder.Services.AddSettlementInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseSwiftBetsObservability();
app.UseSwiftBetsWeb();
app.UseAuthentication();
app.UseAuthorization();
app.MapSwiftBetsOperationalEndpoints();
app.MapSettlementEndpoints();
app.MapGrpcService<CashoutGrpcService>();
app.MapSwiftBetsFaultEndpoints();

await app.RunAsync();
return 0;

public partial class Program;
