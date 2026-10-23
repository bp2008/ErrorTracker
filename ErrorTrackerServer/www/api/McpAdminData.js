import ExecAPI from 'appRoot/api/api';

export function GetAllTokens()
{
	return ExecAPI("McpAdminData/GetAllTokens");
}
export function RevokeAnyToken(apiTokenId)
{
	return ExecAPI("McpAdminData/RevokeToken", { apiTokenId });
}
export function SetSensitiveRead(apiTokenId, grant)
{
	return ExecAPI("McpAdminData/SetSensitiveRead", { apiTokenId, grant });
}
export function GetRedaction()
{
	return ExecAPI("McpAdminData/GetRedaction");
}
export function SetRedaction(hiddenTagKeys, rules)
{
	return ExecAPI("McpAdminData/SetRedaction", { hiddenTagKeys, rules });
}
export function TestRedaction(hiddenTagKeys, rules, text, tagKey)
{
	return ExecAPI("McpAdminData/TestRedaction", { hiddenTagKeys, rules, text, tagKey });
}
export function GetAuditLog(userName, apiTokenId, toolName, startDate, endDate, limit)
{
	return ExecAPI("McpAdminData/GetAuditLog", { userName, apiTokenId, toolName, startDate, endDate, limit });
}
