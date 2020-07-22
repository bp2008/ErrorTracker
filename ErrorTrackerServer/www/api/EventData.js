import ExecAPI from 'appRoot/api/api';

export function GetEvents(projectName, folderId, startTime, endTime)
{
	return ExecAPI("EventData/GetEvents", { projectName, folderId, startTime, endTime });
}
export function GetEvent(projectName, eventId)
{
	return ExecAPI("EventData/GetEvent", { projectName, eventId });
}
export function MoveEvents(projectName, eventIds, newFolderId)
{
	return ExecAPI("EventData/MoveEvents", { projectName, eventIds, newFolderId });
}
export function DeleteEvents(projectName, eventIds)
{
	return ExecAPI("EventData/DeleteEvents", { projectName, eventIds });
}
export function SetEventsColor(projectName, eventIds, color)
{
	return ExecAPI("EventData/SetEventsColor", { projectName, eventIds, color });
}
export function SetEventsReadState(projectName, eventIds, read)
{
	return ExecAPI("EventData/SetEventsReadState", { projectName, eventIds, read });
}