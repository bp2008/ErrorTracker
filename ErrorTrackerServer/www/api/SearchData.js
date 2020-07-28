import ExecAPI from 'appRoot/api/api';
import PreprocessEventSummaries from 'appRoot/api/EventData';

export function SearchSimple(projectName, folderId, query)
{
	return PreprocessEventSummaries(ExecAPI("SearchData/SearchSimple", { projectName, folderId, query }));
}
export function SearchAdvanced(projectName, folderId, matchAll, conditions)
{
	return PreprocessEventSummaries(ExecAPI("SearchData/SearchAdvanced", { projectName, folderId, matchAll, conditions }));
}