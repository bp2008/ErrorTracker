import ExecAPI from 'appRoot/api/api';

export function GetUserData()
{
	return ExecAPI("UserData/GetUserData");
}
export function SetUserData(userName, userData)
{
	return ExecAPI("UserData/SetUserData", { userName, userData });
}
export function AddUser(userData)
{
	return ExecAPI("UserData/AddUser", { userData });
}
export function RemoveUser(userName)
{
	return ExecAPI("UserData/RemoveUser", { userName });
}