import ExecAPI from 'appRoot/api/api';

export function GetSettingsData()
{
	return ExecAPI("SettingsData/GetSettingsData");
}
export function SetSettingsData(settings)
{
	return ExecAPI("SettingsData/SetSettingsData", { settings });
}
export function RestartServer()
{
	return ExecAPI("SettingsData/RestartServer");
}