import ExecAPI from 'appRoot/api/api';

export function SearchSimple(projectName, folderId, query)
{
	return ExecAPI("SearchData/SearchSimple", { projectName, folderId, query });
}
export function SearchAdvanced(projectName, folderId, matchAll, conditions)
{
	return ExecAPI("SearchData/SearchAdvanced", { projectName, folderId, matchAll, conditions });
}