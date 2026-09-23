using System;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.Threading.Tasks;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.MultiTenancy;

namespace ToolShare.Notifications;

/// <summary>
/// Test-only <see cref="IEmailSender"/> substitute (research R2) — records
/// every call in-memory instead of touching a real mail transport, so tests
/// can assert on what would have been sent (US2) without any network
/// dependency, and can simulate a delivery failure (FR-010) via
/// <see cref="ThrowOnSend"/>.
/// </summary>
public class FakeEmailSender : EmailSenderBase, ISingletonDependency
{
    public ConcurrentQueue<MailMessage> SentMails { get; } = new();

    public bool ThrowOnSend { get; set; }

    public FakeEmailSender(ICurrentTenant currentTenant, IEmailSenderConfiguration configuration, IBackgroundJobManager backgroundJobManager)
        : base(currentTenant, configuration, backgroundJobManager)
    {
    }

    protected override Task SendEmailAsync(MailMessage mail)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Simulated mail transport failure (test double).");
        }

        SentMails.Enqueue(mail);
        return Task.CompletedTask;
    }
}
