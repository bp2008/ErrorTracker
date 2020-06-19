import ExecAPI from 'appRoot/api/api';

export function GetAllFolders(projectName)
{
	return ExecAPI("FolderData/GetAllFolders", { projectName });
}
export function GetFolderStructure(projectName)
{
	return ExecAPI("FolderData/GetFolderStructure", { projectName });
}
export function AddFolder(projectName, folderName, parentFolderId)
{
	return ExecAPI("FolderData/AddFolder", { projectName, folderName, parentFolderId });
}
export function MoveFolder(projectName, folderId, newParentFolderId)
{
	return ExecAPI("FolderData/MoveFolder", { projectName, folderId, newParentFolderId });
}
export function RenameFolder(projectName, folderId, newFolderName)
{
	return ExecAPI("FolderData/RenameFolder", { projectName, folderId, newFolderName });
}
export function DeleteFolder(projectName, folderId)
{
	return ExecAPI("FolderData/DeleteFolder", { projectName, folderId });
}
export function RunFilterOnFolder(projectName, filterId, folderId)
{
	return ExecAPI("FolderData/RunFilterOnFolder", { projectName, filterId, folderId });
}
export function RunEnabledFiltersOnFolder(projectName, folderId)
{
	return ExecAPI("FolderData/RunEnabledFiltersOnFolder", { projectName, folderId });
}