using Tgysm.Infrastructure.Email;
using Tgysm.Worker.Jobs;

namespace Tgysm.Worker;

/// <summary>Cross-section startup checks on <see cref="WorkerOptions"/> (run through <c>ValidateOnStart</c>).</summary>
public static class WorkerOptionsValidation
{
    public const string EmailSenderLeaseErrorMessage =
        "Worker:Jobs:EmailSender:LeaseSeconds must be longer than Email:Smtp:TimeoutSeconds, or another run could re-claim an email that is still being sent.";

    /// <summary>
    /// The email sender re-claims each row for one lease right before sending it, and a send is cut off after the
    /// SMTP timeout; the lease must be strictly longer, so a row is never re-claimed while its send is in flight.
    /// </summary>
    public static bool EmailSenderLeaseOutlastsSmtpTimeout(WorkerOptions worker, EmailOptions email) =>
        EmailSenderJob.ResolveLease(worker) > email.Smtp.EffectiveTimeout;
}
