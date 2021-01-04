import { GetVapidPublicKey } from 'appRoot/api/PushData';
import EventBus from 'appRoot/scripts/EventBus';

function CheckServiceWorkerPreconditions()
{
	if (!appContext.serviceWorkerEnabled)
	{
		console.log("The service worker is not enabled in this Error Tracker instance, therefore PUSH notifications will not be available.");
		return false;
	}
	if (!('serviceWorker' in navigator))
	{
		console.warn("Service workers are not supported in this browser, therefore PUSH notifications will not be available.");
		return false;
	}
	if (!('showNotification' in ServiceWorkerRegistration.prototype))
	{
		console.warn("ServiceWorker Notifications are not supported in this browser, therefore PUSH notifications will not be available.");
		return false;
	}
	if (!('PushManager' in window))
	{
		console.warn("Push messaging is not supported in this browser.");
		return false;
	}
	if (!IsSecure())
	{
		console.warn("Notifications and push messaging is not supported because this connection is not secure.");
		return false;
	}
	if (typeof Notification === "undefined")
	{
		console.warn('typeof Notification === "undefined". Push notifications will be unavailable.');
		return false;
	}
	return true;
}

export function ServiceWorkerInit()
{
	// Set up a service worker to handle incoming push notifications.
	if (!CheckServiceWorkerPreconditions())
		return;
	EventBus.notificationPermission = Notification.permission;
	GetServiceWorkerRegistration()
		.then(registration =>
		{
			serviceWorkerRegistration = registration;
			EventBus.pushNotificationsAvailable = true;
			return registration.pushManager.getSubscription()
				.then(subscription =>
				{
					if (subscription)
						EventBus.pushSubscription = subscription;
					return subscription;
				});
		})
		.catch(err =>
		{
			console.error(err);
		});

	// Cleanup previous bad service worker scope:
	navigator.serviceWorker.getRegistrations().then(registrations =>
	{
		for (let i = 0; i < registrations.length; i++)
		{
			if (registrations[i].scope.indexOf("/serviceworker/") > -1)
				registrations[i].unregister();
		}
	});
}
let serviceWorkerRegistration = null;
/**
 * Returns a promise that resolves with the service worker registration object or otherwise rejects.
 * As side-effects, this method may set EventBus.pushNotificationsAvailable and EventBus.pushSubscription.
 */
export async function GetServiceWorkerRegistration()
{
	let registration = serviceWorkerRegistration;
	if (registration === null)
	{
		if (!CheckServiceWorkerPreconditions())
			throw new Error("Service worker preconditions not met.");

		registration = await navigator.serviceWorker.register(appContext.appPath + 'service-worker.js');
		registration = await navigator.serviceWorker.ready;
	}
	return registration;
}

function IsSecure()
{
	if (location.host === "127.0.0.1" || location.host === "localhost" || location.protocol === "https:")
		return true;
	return false;
}