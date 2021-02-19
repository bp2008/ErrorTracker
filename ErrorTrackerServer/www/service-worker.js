// Register event listener for the 'push' event.
self.addEventListener('push', function (event)
{
	var data;
	if (event.data)
		data = event.data.json();
	else
		data = { title: "Error Tracker", message: "PUSH notification failure. No message was received." };

	// Prepend project name before title.
	var title = (data.project ? data.project + " - " : "") + data.title;

	// Keep the service worker alive until the notification is created.
	event.waitUntil(
		// Show a notification with a title and a body
		self.registration.showNotification(title, { body: data.message, data: data })
	);
});
self.addEventListener('notificationclick', function (event)
{
	//console.log('On notification click: ', event.notification);
	event.notification.close();

	// Construct a path to the thing this notification is telling us about.
	var path = 'client/home';
	if (event.notification.data.project)
	{
		path += '?p=' + encodeURIComponent(event.notification.data.project);
		if (IsNonnegativeNumber(event.notification.data.folderid))
		{
			path += '&f=' + encodeURIComponent(event.notification.data.folderid);
			if (IsNonnegativeNumber(event.notification.data.eventid))
			{
				path += '&e=' + encodeURIComponent(event.notification.data.eventid) + '&se=' + encodeURIComponent(event.notification.data.eventid);
			}
		}
	}
	var pathLower = path.toLowerCase();

	// Open existing if we can find it before creating new window.
	// This is a relatively primitive matching method which won't work if arguments are added or reordered.
	event.waitUntil(clients.matchAll({
		type: "window"
	}).then(function (clientList)
	{
		for (var i = 0; i < clientList.length; i++)
		{
			var client = clientList[i];
			if (client.url.toLowerCase().indexOf(pathLower) > -1 && 'focus' in client)
				return client.focus();
		}
		if (clients.openWindow)
			return clients.openWindow(path);
	}));
});
function IsNonnegativeNumber(obj)
{
	if (typeof obj === "undefined")
		return false;
	if (obj === null)
		return false;
	if (isNaN(obj))
		return false;
	if (obj > -1)
		return true;
	return false;
}