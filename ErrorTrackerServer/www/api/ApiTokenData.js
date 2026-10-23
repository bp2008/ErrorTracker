import ExecAPI from 'appRoot/api/api';

export function GetMyTokens()
{
	return ExecAPI("ApiTokenData/GetMyTokens");
}
export function CreateToken(name, scopes, projects, expires)
{
	return ExecAPI("ApiTokenData/CreateToken", { name, scopes, projects, expires });
}
export function RevokeMyToken(apiTokenId)
{
	return ExecAPI("ApiTokenData/RevokeToken", { apiTokenId });
}
