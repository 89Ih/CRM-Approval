using System;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace Project.Approval
{
    internal static class MailHelper
    {
        public static void SendEmail(IOrganizationService service, ITracingService tracing,
            Guid senderId, Guid recipientId, string subject, string description)
        {
            try
            {
                var email = new Entity("email")
                {
                    ["subject"] = subject ?? string.Empty,
                    ["description"] = description ?? string.Empty,
                    ["directioncode"] = true
                };

                // From (Sender)
                email["from"] = new[]
                {
                    new Entity("activityparty")
                    {
                        ["partyid"] = new EntityReference("systemuser", senderId)
                    }
                };

                // To (Recipient)
                email["to"] = new[]
                {
                    new Entity("activityparty")
                    {
                        ["partyid"] = new EntityReference("systemuser", recipientId)
                    }
                };

                Guid emailId = service.Create(email);

                service.Execute(new SendEmailRequest
                {
                    EmailId = emailId,
                    IssueSend = true
                });

                tracing.Trace($"Email {emailId} sent successfully.");
            }
            catch (Exception ex)
            {
                tracing.Trace($"MailHelper.SendEmail error: {ex}");
                throw;
            }
        }

        public static string BuildMailDescription(string title, string requestedBy, string approverName, string recordUrl)
        {
            return $@"
                Hello {approverName},<br/><br/>
                A new approval request requires your attention:<br/><br/>
                <ul>
                    <li>Item: {title}</li>
                    <li>Requested by: {requestedBy}</li>
                    <li>Purpose: [Short description]</li>
                </ul>
                Please review and approve at your earliest convenience:<br/>
                👉 <a href='{recordUrl}' target='_blank' rel='noopener noreferrer'>Open Record</a><br/><br/>
                Thank you";
        }
    }
}
