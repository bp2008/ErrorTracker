import ExecAPI from 'appRoot/api/api';

export function GetLoginRecordsForSelf()
{
	return ExecAPI("LoginRecordData/GetLoginRecordsForSelf");
}
export function GetLoginRecordsForUser(userName)
{
	return ExecAPI("LoginRecordData/GetLoginRecordsForUser", { userName });
}
export function GetLoginRecordsGlobal(startDate, endDate)
{
	return ExecAPI("LoginRecordData/GetLoginRecordsGlobal", { startDate, endDate });
}
export function GeolocateIP(ip)
{
	return ExecAPI("LoginRecordData/GeolocateIP", { ip });
}