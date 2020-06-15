import ExecAPI from 'appRoot/api/api';

export function GetEventsByDate(projectName, startTime, endTime)
{
	return ExecAPI("EventData/GetEventsByDate", { projectName, startTime, endTime });
}
export function GetEventsInFolder(projectName, folderId, startTime, endTime)
{
	return ExecAPI("EventData/GetEventsInFolder", { projectName, folderId, startTime, endTime });
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