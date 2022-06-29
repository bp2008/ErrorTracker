import ExecAPI from 'appRoot/api/api';
import EventBus from 'appRoot/scripts/EventBus';

export function GetEvents(projectName, folderId, startTime, endTime, uniqueOnly)
{
	return PreprocessEventSummaries(ExecAPI("EventData/GetEvents", { projectName, folderId, startTime, endTime, uniqueOnly }));
}
export function PreprocessEventSummaries(promise)
{
	return promise
		.then(data =>
		{
			// Preprocess events array:
			if (data.success && data.events && data.events.length)
				for (let i = 0; i < data.events.length; i++)
				{
					// Teach EventBus
					EventBus.learnEventSummary(data.events[i]);

					//// Add FolderId fields.
					//data.events[i].FolderId = folderId;
				}
			return data;
		});
}
export function GetEvent(projectName, eventId)
{
	return ExecAPI("EventData/GetEvent", { projectName, eventId });
}
export function MoveEvents(projectName, eventIds, newFolderId)
{
	return ExecAPI("EventData/MoveEvents", { projectName, eventIds, newFolderId });
}
export function MoveEventsMap(projectName, eventIdToNewFolderId)
{
	return ExecAPI("EventData/MoveEventsMap", { projectName, eventIdToNewFolderId });
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
export function CountUnreadEventsByFolder(projectName)
{
	return ExecAPI("EventData/CountUnreadEventsByFolder", { projectName });
}
export function SetEventListCustomTagKey(projectName, eventListCustomTagKey)
{
	return ExecAPI("EventData/SetEventListCustomTagKey", { projectName, eventListCustomTagKey });
}
export function CopyEventsToProject(projectName, eventIds, projectDest)
{
	return ExecAPI("EventData/CopyEventsToProject", { projectName, eventIds, targetProjectName: projectDest });
}