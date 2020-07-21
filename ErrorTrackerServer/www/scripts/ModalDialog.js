import ModalDialogComponent from 'appRoot/vues/common/controls/ModalDialog.vue';
const makeModalDialog = create({ component: ModalDialogComponent, wrapper: 'dialogFade' });

import ConfirmPopup from 'appRoot/vues/common/controls/ConfirmPopup.vue';
import TextInputPopup from 'appRoot/vues/common/controls/TextInputPopup.vue';
import ColorInputPopup from 'appRoot/vues/common/controls/ColorInputPopup.vue';
import SelectFilterPopup from 'appRoot/vues/client/controls/SelectFilterPopup.vue';

//////////////////////////////////////////////////
// Container Registration / Dialog Registration //
//////////////////////////////////////////////////
let allContainers = {};
export function RegisterModalDialogContainer(containerComponent)
{
	if (allContainers[containerComponent.name] !== containerComponent)
		allContainers[containerComponent.name] = containerComponent;
}
export function UnregisterModalDialogContainer(containerComponent)
{
	if (allContainers[containerComponent.name] === containerComponent)
	{
		delete allContainers[containerComponent.name];
	}
}
/**
 * Returns a function to create a dialog from the specified component, in the specified container.
 * @param {Object} param0 An object containing two properties. [component] should be a reference to a component (suggestion: load via "import"). [wrapper] should be a string name of a ModalDialogContainer element that has been added to the root vue component.
 * @returns {Function} Returns a function.  The function accepts as an argument an object defining the props to be passed to the created component.  The function returns a promise which resolves when the dialog is closed.
 */
function create({ component, wrapper })
{
	return props =>
	{
		return new Promise((resolve /* this never rejects */) =>
		{
			let container = allContainers[wrapper];
			if (!container)
			{
				console.error('Dialog container "' + wrapper + '" does not exist. Make sure you have added <ModalDialogContainer name="' + wrapper + '" /> somewhere in the project.');
				resolve(false);
				return;
			}
			container.CreateDialog(component, props, dialogResult =>
			{
				// Called upon dialog close
				resolve(dialogResult);
			});
		});
	};
}
/**
 * Closes all open dialogs.
 */
export function CloseAllDialogs()
{
	for (let key in allContainers)
		if (allContainers.hasOwnProperty(key))
		{
			let c = allContainers[key];
			if (c && typeof c.CloseAllDialogs === "function")
				c.CloseAllDialogs();
		}
}
export function CountOpenDialogs(condition)
{
	let total = 0;
	for (let key in allContainers)
		if (allContainers.hasOwnProperty(key))
		{
			let c = allContainers[key];
			if (c && c.components)
			{
				if (typeof condition === "function")
				{
					for (let i = 0; i < c.components.length; i++)
						if (condition(c.components[i]))
							total++;
				}
				else if (c.components.length)
					total += c.components.length;
			}
		}
	return total;
}
///////////////////////////////
// Dialog-Creation Functions //
///////////////////////////////
/**
 * Creates a modal dialog containing the specified component, passing along the specified props.
 * Returns a promise which resolves when the dialog closes. Does not reject.
 * @param {any} contentComponent A vue component to serve as the content component for the dialog (suggestion: get this via an import statement).
 * @param {Object} contentProps Props to be passed to the content component.
 * @param {Object} options Options for the dialog.
 * @returns {Promise} Returns a promise which resolves when the dialog closes. Does not reject.
 */
export function ModalDialog(contentComponent, contentProps, options)
{
	options = Object.assign({
		zIndex: null, // (default is defined in ModalDialog.vue's style block). Beware that setting this will break normal dialog ordering where the last dialog opened is the top dialog.
		positionAbsolute: false,
		halfHeight: false
	}, options);

	let args = {
		contentComponent,
		contentProps,
		zIndex: options.zIndex,
		positionAbsolute: options.positionAbsolute,
		halfHeight: options.halfHeight
	};
	if (!args.contentComponent)
		console.error("No valid contentComponent was provided to ModalDialog function");
	return makeModalDialog(args);
}
/**
 * Creates a modal message dialog, returning a promise which resolves when the dialog closes. Does not reject.
 * @param {String} message A message for the dialog.
 * @param {String} title Optional title for the dialog.
 * @param {Object} props Optional object containing additional properties for the dialog.
 * @param {Object} options Optional object containing options for the dialog.
 * @returns {Promise} Returns a promise which resolves when the dialog closes. Does not reject.
 */
export function ModalMessageDialog(message, title, props = null, options = null)
{
	let args = { message: message };
	if (props)
		Object.assign(args, props);
	if (typeof title !== "undefined")
		args.title = title;

	return ModalDialog(ConfirmPopup, args, options);
}
/**
 * Creates a modal confirm dialog, returning a promise which resolves when the dialog closes. Does not reject.
 * The resolve value is true if the user clicked the accept button.
 * @param {any} message A string message for the dialog. Or, optionally, an args object (for advanced use).
 * @param {String} title Optional title for the dialog.
 * @param {String} yesText Text to show in the accept button.
 * @param {String} noText Text to show in the decline button.
 * @returns {Promise} Returns a promise which resolves when the dialog closes. Does not reject.
 */
export function ModalConfirmDialog(message, title, yesText, noText)
{
	let args;
	if (typeof message === "object")
	{
		args = message;
		args.confirm = true;
	}
	else
	{
		args = { message: message, confirm: true };
		if (typeof title !== "undefined")
			args.title = title;
		if (typeof yesText !== "undefined")
			args.yesText = yesText;
		if (typeof noText !== "undefined")
			args.noText = noText;
	}
	return ModalDialog(ConfirmPopup, args);
}
/**
 * Opens a dialog box with a text input field inside, and returns a promise that resolves with an object { value: "input text" }, or false if the dialog was canceled.
 * Returns a promise which resolves when the dialog closes. Does not reject.
 * @param {any} title Optional title for the dialog box. Appears specially styled. (omitted if null or empty). May optionally be an object containing all the arguments for the text input dialog.
 * @param {String} message Optional message for the dialog box. (omitted if null or empty)
 * @param {String} placeholder Optional placeholder text for the text input. (omitted if null or empty)
 * @param {String} initialText Optional text that should be in the text input when it first appears.
 * @param {String} checkboxText If provided, a checkbox will be inserted into the dialog box with this text, and the resolve value will also have a "checked" field.
 * @returns {Promise} Returns a promise which resolves when the dialog closes. Does not reject.
 */
export function TextInputDialog(title, message, placeholder, initialText, checkboxText)
{
	let args;
	if (typeof title === "object")
		args = title;
	else
	{
		args = { title: title };
		if (typeof message !== "undefined") args.message = message;
		if (typeof placeholder !== "undefined") args.placeholder = placeholder;
		if (typeof initialText !== "undefined") args.initialText = initialText;
		if (typeof checkboxText !== "undefined") args.checkboxText = checkboxText;
	}
	return ModalDialog(TextInputPopup, args);
}
/**
 * Opens a dialog box with a color input field inside, and returns a promise that resolves with an object { value: "F0F0F0" }, or false if the dialog was canceled.
 * Returns a promise which resolves when the dialog closes. Does not reject.
 * @param {String} title Optional title for the dialog box. Appears specially styled. (omitted if null or empty).
 * @param {String} message Optional message for the dialog box. (omitted if null or empty)
 * @param {String} initialColor Optional hex color (not including #) that should be in the color input when it first appears.
 * @returns {Promise} Returns a promise which resolves when the dialog closes. Does not reject.
 */
export function ColorInputDialog(title, message, initialColor)
{
	let args;
	args = { title: title };
	if (typeof message !== "undefined") args.message = message;
	if (typeof initialColor !== "undefined") args.initialColor = initialColor;
	return ModalDialog(ColorInputPopup, args);
}
/**
 * Opens a dialog box where the user can select a Filter. Returns a promise that resolves with the selected Filter, or a falsy value if canceled.  The promise does not reject.
 * @param {String} projectName Name of the project to load Filters from.
 * @returns {Promise} Returns a promise which resolves when the dialog closes. Does not reject.
 */
export function SelectFilterDialog(projectName)
{
	let args = { projectName };
	return ModalDialog(SelectFilterPopup, args);
}