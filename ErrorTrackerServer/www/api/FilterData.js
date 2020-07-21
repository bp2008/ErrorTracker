import ExecAPI from 'appRoot/api/api';

export function GetAllFilters(projectName)
{
	return ExecAPI("FilterData/GetAllFilters", { projectName });
}
export function GetFilter(projectName, filterId)
{
	return ExecAPI("FilterData/GetFilter", { projectName, filterId });
}
export function AddFilter(projectName, name, conditions, conditionHandling)
{
	return ExecAPI("FilterData/AddFilter", { projectName, name, conditions, conditionHandling });
}
export function EditFilter(projectName, filter)
{
	return ExecAPI("FilterData/EditFilter", { projectName, filter });
}
export function DeleteFilter(projectName, filterId)
{
	return ExecAPI("FilterData/DeleteFilter", { projectName, filterId });
}
export function AddCondition(projectName, condition)
{
	return ExecAPI("FilterData/AddCondition", { projectName, condition });
}
export function EditCondition(projectName, condition)
{
	return ExecAPI("FilterData/EditCondition", { projectName, condition });
}
export function DeleteCondition(projectName, condition)
{
	return ExecAPI("FilterData/DeleteCondition", { projectName, condition });
}
export function AddAction(projectName, action)
{
	return ExecAPI("FilterData/AddAction", { projectName, action });
}
export function EditAction(projectName, action)
{
	return ExecAPI("FilterData/EditAction", { projectName, action });
}
export function DeleteAction(projectName, action)
{
	return ExecAPI("FilterData/DeleteAction", { projectName, action });
}
export function ReorderFilters(projectName, newOrder)
{
	return ExecAPI("FilterData/ReorderFilters", { projectName, newOrder });
}
export function RunFilterAgainstAllEvents(projectName, filterId)
{
	return ExecAPI("FilterData/RunFilterAgainstAllEvents", { projectName, filterId });
}
export function RunEnabledFiltersAgainstAllEvents(projectName)
{
	return ExecAPI("FilterData/RunEnabledFiltersAgainstAllEvents", { projectName });
}