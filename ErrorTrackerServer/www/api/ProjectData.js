import ExecAPI from 'appRoot/api/api';

export function GetProjectData()
{
	return ExecAPI("ProjectData/GetProjectData");
}
export function AddProject(projectName)
{
	return ExecAPI("ProjectData/AddProject", { projectName });
}
export function RemoveProject(projectName)
{
	return ExecAPI("ProjectData/RemoveProject", { projectName });
}
export function ReplaceSubmitKey(projectName)
{
	return ExecAPI("ProjectData/ReplaceSubmitKey", { projectName });
}