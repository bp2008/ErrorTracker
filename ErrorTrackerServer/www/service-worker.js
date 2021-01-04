// Register event listener for the 'push' event.
self.addEventListener('push', function (event)
{
	var data;
	if (event.data)
		data = event.data.json();
	else
		data = { title: "Error Tracker", message: "PUSH notification failure. No message was received." };

	// Keep the service worker alive until the notification is created.
	event.waitUntil(
		// Show a notification with a title and a body
		self.registration.showNotification(data.title, { body: data.message })
	);
});