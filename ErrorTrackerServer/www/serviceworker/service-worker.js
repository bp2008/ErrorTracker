// Register event listener for the 'push' event.
self.addEventListener('push', function (event)
{
	// Keep the service worker alive until the notification is created.
	event.waitUntil(
		// Show a notification with a title and a body
		self.registration.showNotification(
			'ErrorTrackerServer Test',
			{ body: "This is a test notification message" }
		)
	);
});