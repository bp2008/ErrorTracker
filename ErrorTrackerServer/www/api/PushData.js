import ExecAPI from 'appRoot/api/api';
import EventBus from 'appRoot/scripts/EventBus';

export function GetVapidPublicKey()
{
	return ExecAPI("PushData/GetVapidPublicKey");
}
export function RegisterForPush(projectName, folderId, subscriptionKey)
{
	return ExecAPI("PushData/RegisterForPush", { projectName, folderId, subscriptionKey });
}
export function UnregisterForPush(projectName, folderId, subscriptionKey)
{
	return ExecAPI("PushData/UnregisterForPush", { projectName, folderId, subscriptionKey });
}
export function GetRegistrationStatus(projectName, folderId, subscriptionKey)
{
	return ExecAPI("PushData/GetRegistrationStatus", { projectName, folderId, subscriptionKey });
}