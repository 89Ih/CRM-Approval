using System;
using System.Linq;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Organization;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Project.Approval
{
    public class HandleApprovement : IPlugin
    {
        private string environmentUrl = string.Empty;
        private IOrganizationService _service;
        private ITracingService _tracing;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            // store globally
            _service = serviceFactory.CreateOrganizationService(context.UserId);
            _tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            try
            {
                if (!(context.InputParameters.TryGetValue("Target", out var target) && target is Entity customerAgreement))
                    return;

                if (customerAgreement.LogicalName != "pr_customeragreement")
                    return;

                // Get environment URL once
                if (string.IsNullOrEmpty(environmentUrl))
                {
                    var resp = (RetrieveCurrentOrganizationResponse)_service.Execute(new RetrieveCurrentOrganizationRequest());
                    if (resp?.Detail?.Endpoints != null &&
                        resp.Detail.Endpoints.TryGetValue(EndpointType.WebApplication, out var webAppUrl) &&
                        Uri.TryCreate(webAppUrl, UriKind.Absolute, out _))
                    {
                        environmentUrl = webAppUrl.TrimEnd('/');
                    }
                    else
                    {
                        _tracing.Trace("WebApplication endpoint not found; record URL will be omitted.");
                    }
                }

                Entity fullEntity = null;
                if (context.MessageName == "Create")
                {
                    fullEntity = customerAgreement;
                }
                else if (context.MessageName == "Update")
                {
                    fullEntity = _service.Retrieve(customerAgreement.LogicalName, customerAgreement.Id, new ColumnSet());
                }

                if (fullEntity == null)
                    return;

                if (!GeneralValidation(fullEntity))
                {
                    _tracing.Trace("Approval creation not possible: conditions not met.");
                    return;
                }

                var agreementType = fullEntity.GetAttributeValue<OptionSetValue>("pr_customeragreement");
                switch (agreementType?.Value)
                {
                    case 125620000:
                        CreateApprove(customerAgreement.LogicalName, customerAgreement.Id, fullEntity, true, Guid.Empty);
                        break;
                    case 125620001:
                    case 125620002:
                        // Reserved for future cases
                        break;
                    default:
                        _tracing.Trace($"Unhandled pr_customeragreement value: {agreementType?.Value}");
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    $"An error occurred in the HandleApprovement plugin: {ex.Message}", ex);
            }
        }

        private string BuildRecordUrl(string entityLogicalName, Guid id) =>
            string.IsNullOrWhiteSpace(environmentUrl)
                ? null
                : $"{environmentUrl}/main.aspx?etn={entityLogicalName}&id={id}&pagetype=entityrecord";

        private static bool IsEmptyOption(OptionSetValue v) => v == null || v.Value == -1;

        private static bool GeneralValidation(Entity row)
        {
            if (row == null) return false;

            bool entryCompleted = row.GetAttributeValue<bool>("pr_entrycompleted");

            bool noOptionValues =
                !IsEmptyOption(row.GetAttributeValue<OptionSetValue>("pr_customeragreement")) &&
                IsEmptyOption(row.GetAttributeValue<OptionSetValue>("pr_m1decision")) &&
                IsEmptyOption(row.GetAttributeValue<OptionSetValue>("pr_m2decision")) &&
                IsEmptyOption(row.GetAttributeValue<OptionSetValue>("pr_m3decision")) &&
                IsEmptyOption(row.GetAttributeValue<OptionSetValue>("pr_m4decision"));

            return entryCompleted && noOptionValues;
        }

        private void CreateApprove(string parentLogicalName, Guid parentId, Entity parent, bool twoStage, Guid approver)
        {
            string agreementTitle = parent.GetAttributeValue<string>("pr_jobtitle") ?? string.Empty;
            string recordUrl = BuildRecordUrl(parentLogicalName, parentId);
            string title = twoStage ? $"Erste Freigabe - {agreementTitle}" : agreementTitle;

            var parentApprove = new Entity("pr_approvement")
            {
                ["pr_name"] = title,
                ["pr_entitytraget"] = parentLogicalName,      // ⚠ check schema name
                ["pr_entitytragetguid"] = parentId.ToString(),
                ["pr_twostageapproval"] = twoStage,
                ["ownerid"] = new EntityReference("systemuser", approver)
            };

            if (!string.IsNullOrWhiteSpace(recordUrl))
                parentApprove["pr_recordurl"] = recordUrl;

            var created = _service.Create(parentApprove);
            if (created == Guid.Empty) return;

            var senderRef = parent.GetAttributeValue<EntityReference>("pr_owner");

            MailHelper.SendEmail(
                _service,
                _tracing,
                senderRef?.Id ?? Guid.Empty,
                approver,
                "Approval Request",
                MailHelper.BuildMailDescription(title, senderRef?.Name ?? string.Empty, "GL", recordUrl ?? string.Empty)
            );

            _tracing.Trace("Approval created successfully.");
        }

        private void RetrieveApprover()
        {
            QueryExpression query = new QueryExpression("pr_setting")
            {
                ColumnSet = new ColumnSet("pr_settingid", "pr_parameter", "pr_textaria"),
                Criteria = new FilterExpression
                {
                    Conditions =
            {
                new ConditionExpression("pr_parameter", ConditionOperator.Equal, "PARA")
            }
                }
            };

            EntityCollection settings = _service.RetrieveMultiple(query);

            if (settings.Entities.Count == 0)
                return;

            Entity setting = settings.Entities.First();

            string approverStringObject = setting.GetAttributeValue<string>("pr_textaria");

            if (string.IsNullOrWhiteSpace(approverStringObject))
            {
                _tracing.Trace("pr_textaria is empty.");
                return;
            }

            try
            {
                JToken approverJson;

                if (approverStringObject.TrimStart().StartsWith("{") || approverStringObject.TrimStart().StartsWith("["))
                {
                    // Already JSON → parse it
                    approverJson = JToken.Parse(approverStringObject);
                }
                else
                {
                    // Wrap it as JSON
                    approverJson = new JObject { ["value"] = approverStringObject };
                }

                string jsonOutput = approverJson.ToString(Formatting.Indented);

                _tracing.Trace("Approver JSON: " + jsonOutput);
            }
            catch (JsonException ex)
            {
                _tracing.Trace($"Invalid JSON in pr_textaria: {ex.Message}");
            }
        }

    }
}
