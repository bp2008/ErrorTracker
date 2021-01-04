import EventBus from 'appRoot/scripts/EventBus';
import { GetVapidPublicKey, GetRegistrationStatus, RegisterForPush, UnregisterForPush } from 'appRoot/api/PushData';
import { GetServiceWorkerRegistration } from 'appRoot/scripts/ServiceWorkerInit';
import { bytesToHex } from 'appRoot/scripts/Util';


/**
 * Returns a promise that resolves with true if this browser is registered for push in the specified folder. If not registered, resolves with false.  The promise does not reject.
 * @param {String} projectName Project Name
 * @param {String} folderId Folder ID
 */
export function IsRegisteredForPush(projectName, folderId)
{
	return new Promise((resolve, reject) =>
	{
		if (!EventBus.pushNotificationsAvailable)
			return resolve(false);

		if (EventBus.pushSubscription === null)
			return resolve(false);

		if (EventBus.notificationPermission !== "granted")
			return resolve(false);

		return GetRegistrationStatus(projectName, folderId, JSON.stringify(EventBus.pushSubscription))
			.then(result =>
			{
				return resolve(result.subscribed);
			})
			.catch(err =>
			{
				toaster.error(err);
				return resolve(false);
			});
	});
}

export async function RegisterPushNotificationsForFolder(projectName, folderId)
{
	if (!EventBus.pushNotificationsAvailable)
		throw new Error("Push notifications are not available. See developer console.");

	if (EventBus.notificationPermission === "denied")
		throw new Error("You have previously denied notification permission for this site.  Therefore, you must edit the permission manually in your browser's settings.");

	if (EventBus.notificationPermission === "default")
		await Notification.requestPermission()
			.then(result =>
			{
				EventBus.notificationPermission = Notification.permission;
			});

	if (EventBus.notificationPermission !== "granted")
		throw new Error("Notification permission was not granted.");


	// Get the server's public key
	const vapidKeyResponse = await GetVapidPublicKey();

	// Old versions of Chrome do not accept the vapidPublicKey as base64.
	const convertedVapidKey = urlBase64ToUint8Array(vapidKeyResponse.vapidPublicKey);

	if (EventBus.pushSubscription && EventBus.pushSubscription.options && EventBus.pushSubscription.options.applicationServerKey && Util.bytesToHex(new Uint8Array(EventBus.pushSubscription.options.applicationServerKey)) !== bytesToHex(convertedVapidKey))
	{
		console.log("Server VAPID key change detected. Unsubscribing from previous push subscription and resubscribing.");
		let unsubscribe_success = await EventBus.pushSubscription.unsubscribe();
		EventBus.pushSubscription = null;
	}

	// Get push subscription key
	if (!EventBus.pushSubscription)
	{
		// Subscribe the user (userVisibleOnly allows to specify that we don't plan to send notifications that don't have a visible effect for the user).
		const registration = await GetServiceWorkerRegistration();

		let subscription = await registration.pushManager.getSubscription();

		if (!subscription)
			subscription = await registration.pushManager.subscribe({
				userVisibleOnly: true,
				applicationServerKey: convertedVapidKey
			});

		if (subscription)
			EventBus.pushSubscription = subscription;
	}

	if (EventBus.pushSubscription)
		return await RegisterForPush(projectName, folderId, JSON.stringify(EventBus.pushSubscription));

	return null;
}

export async function UnregisterPushNotificationsForFolder(projectName, folderId)
{
	if (!EventBus.pushNotificationsAvailable)
		throw new Error("Push notifications are not available. See developer console.");

	// Get push subscription key
	if (!EventBus.pushSubscription)
	{
		// Subscribe the user (userVisibleOnly allows to specify that we don't plan to send notifications that don't have a visible effect for the user).
		const registration = await GetServiceWorkerRegistration();

		let subscription = await registration.pushManager.getSubscription();
		if (subscription)
			EventBus.pushSubscription = subscription;
	}

	if (EventBus.pushSubscription)
		return await UnregisterForPush(projectName, folderId, JSON.stringify(EventBus.pushSubscription));

	return null;
}

function urlBase64ToUint8Array(base64String)
{
	var padding = '='.repeat((4 - base64String.length % 4) % 4);
	var base64 = (base64String + padding)
		.replace(/\-/g, '+')
		.replace(/_/g, '/');

	var rawData = window.atob(base64);
	var outputArray = new Uint8Array(rawData.length);

	for (var i = 0; i < rawData.length; ++i)
	{
		outputArray[i] = rawData.charCodeAt(i);
	}
	return outputArray;
}