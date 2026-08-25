using Amazon.SimpleNotificationService;
using Amazon.Extensions.NETCore.Setup;

using BankWithdrawal.Api.Application.Services;
using BankWithdrawal.Api.BackgroundServices;
using BankWithdrawal.Api.Infrastructure;
using BankWithdrawal.Api.Infrastructure.Messaging;
using BankWithdrawal.Api.Infrastructure.Messaging.BankWithdrawal.Api.Infrastructure.Messaging;
using BankWithdrawal.Api.Infrastructure.Outbox;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();
        builder.Services.AddAWSService<IAmazonSimpleNotificationService>();

        builder.Services.AddScoped<IEventPublisher, SnsEventPublisher>();
        builder.Services.AddScoped<WithdrawalService>();
        builder.Services.AddHostedService<OutboxDispatcher>();
        builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
        builder.Services.AddScoped<IAccountRepository, AccountRepository>();
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.MapControllers();

        app.Run();
    }
}
