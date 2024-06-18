import ExecAPI from 'appRoot/api/api';

export function GetProjectList()
{
	return ExecAPI("ProjectList/GetProjectList");
}
export function GetProjectEventCounts()
{
	return ExecAPI("ProjectList/GetProjectEventCounts");
}