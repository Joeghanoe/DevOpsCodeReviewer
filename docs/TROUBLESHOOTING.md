# Troubleshooting Guide

This guide helps diagnose and fix common issues with the Azure DevOps AI Code Reviewer.

## Quick Diagnostics

### 1. Check Health Endpoint

```bash
curl https://your-function.azurewebsites.net/api/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": {
    "serviceBus": { "healthy": true },
    "configuration": { "healthy": true }
  }
}
```

### 2. Check Application Insights

1. Azure Portal → Your Function App → Application Insights
2. Check **Failures** blade for errors
3. Check **Logs** for detailed traces

### 3. Verify Service Hook Delivery

1. Azure DevOps → Project Settings → Service hooks
2. Find your webhook subscription
3. Click to view **Recent events**
4. Check delivery status and response

## Common Issues

### No Comments Appearing on PRs

**Symptoms**: PRs are created but no AI comments appear.

**Possible Causes**:

1. **Webhook not configured correctly**
   - Verify webhook URL is correct
   - Check secret matches Function App setting
   - Test the webhook from Azure DevOps

2. **PAT token expired or invalid**
   ```bash
   # Check Key Vault secret
   az keyvault secret show --vault-name kv-xxx --name ado-pat-token
   ```

3. **Files filtered out**
   - Check if PR contains supported file types
   - Verify files aren't too large (>100KB default)
   - Check excluded patterns

4. **LLM returned no comments**
   - The code may be clean!
   - Lower `Llm__MinSeverityLevel` to 1
   - Check Application Insights for LLM response

**Diagnostic Query** (Application Insights):
```kusto
traces
| where message contains "Generated" and message contains "comments"
| order by timestamp desc
| take 10
```

### Webhook Returns 401 Unauthorized

**Symptoms**: Azure DevOps shows 401 error for webhook.

**Solution**:
1. Verify webhook secret in query parameter matches `Webhook__Secret`
2. Check Basic Auth credentials if using that method
3. Regenerate and update secret in both places

### Webhook Returns 500 Error

**Symptoms**: Azure DevOps shows 500 error for webhook.

**Diagnostic Query**:
```kusto
exceptions
| where cloud_RoleName contains "func-"
| order by timestamp desc
| take 10
```

**Common Causes**:
- Service Bus connection string invalid
- Missing configuration values
- Function App startup failure

### LLM Errors

**Symptoms**: Comments not appearing, errors in logs about LLM.

**Error: "No API key configured"**
```bash
# Add API key to Key Vault
az keyvault secret set \
  --vault-name kv-xxx \
  --name foundry-api-key \
  --value "your-api-key"
```

**Error: "429 Too Many Requests"**
- Azure OpenAI quota exceeded
- Wait or increase quota in Azure Portal

**Error: "Invalid deployment name"**
- Verify `Llm__DeploymentName` matches your Azure OpenAI deployment

### Service Bus Errors

**Symptoms**: Messages not processing, dead letters accumulating.

**Check Dead Letter Queue**:
```bash
# Using Azure Service Bus Explorer or:
az servicebus queue show \
  --namespace-name sb-xxx \
  --name codereview-requests \
  --query "countDetails"
```

**Common Causes**:
- LLM timeout (increase `Llm__TimeoutSeconds`)
- Azure DevOps API failures (check PAT)
- Invalid message format

### Azure DevOps API Errors

**Symptoms**: Errors about "Failed to get pull request" or similar.

**Check PAT Permissions**:
Required scopes:
- Code: Read & Write
- Pull Request Threads: Read & Write

**Verify Organization URL**:
```bash
# Should be https://dev.azure.com/your-org (no trailing slash)
echo $AzureDevOps__OrganizationUrl
```

## Debug Mode

### Enable Detailed Logging

In `host.json`:
```json
{
  "logging": {
    "logLevel": {
      "Function": "Debug",
      "DevOpsCodeReviewer": "Debug"
    }
  }
}
```

### Local Debugging

1. Copy `local.settings.json.example` to `local.settings.json`
2. Fill in your values
3. Start Azurite: `azurite --silent`
4. Start Function: `func start`
5. Use ngrok for webhook: `ngrok http 7071`

## Performance Issues

### Slow Review Processing

**Symptoms**: Reviews take more than 2-3 minutes.

**Solutions**:
1. Reduce `Llm__MaxFilesPerRequest`
2. Reduce `Llm__MaxLinesPerRequest`
3. Increase `Llm__MinSeverityLevel` (fewer comments)
4. Consider Premium plan for pre-warmed instances

### High Costs

**Symptoms**: Unexpected Azure charges.

**Solutions**:
1. Check LLM token usage in Application Insights
2. Reduce max files/lines per request
3. Increase min severity level
4. Use smaller model (gpt-4o-mini)
5. Filter more file types

## Getting Help

### Collect Diagnostic Information

Before reporting issues, gather:

1. **Health check output**:
   ```bash
   curl https://your-function.azurewebsites.net/api/health | jq
   ```

2. **Recent errors** (Application Insights):
   ```kusto
   exceptions
   | where timestamp > ago(1h)
   | project timestamp, message, outerMessage
   ```

3. **Recent traces** (Application Insights):
   ```kusto
   traces
   | where timestamp > ago(1h)
   | where severityLevel >= 3
   | project timestamp, message
   ```

4. **Function App configuration** (sanitized):
   - Which settings are configured
   - Which are missing

### Report Issues

Open an issue at: https://github.com/your-org/azdo-code-reviewer/issues

Include:
- Description of the problem
- Steps to reproduce
- Expected vs actual behavior
- Diagnostic information (sanitized)
