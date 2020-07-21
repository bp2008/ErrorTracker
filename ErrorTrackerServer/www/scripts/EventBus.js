import Vue from 'vue';
/////////////////////////////////////////
// READ THIS BEFORE IMPORTING ANYTHING //
/////////////////////////////////////////
// EventBus is a vue component that is created when this file is imported.
//
// Circular dependencies can create a complex situation where a dependency imported by EventBus.js is not loaded before the EventBus component is created.

// If you need to import something here, make sure that EventBus.js is imported very early in the application initialization.  A good place would be just after importing babel-polyfill (which should typically be the first thing imported).
/////////////////////////////////////////


/**
 * This object can be used to provide "global" event handling.  Based on https://alligator.io/vuejs/global-event-bus/
 * Import the EventBus and use EventBus.$on('eventName', …), EventBus.$off('eventName', …), and EventBus.$emit('EventName', …)
 * Or depending on your need, simply create reactive data using the EventBus data property.
 * The EventBus is good for things that need to be shared between components with fast reactivity and should not be persisted between page reloads.
 * EventBus is orders of magnitude faster than the vuex store.
 */
const EventBus = new Vue({
	data:
	{
		windowWidth: -1,
		windowHeight: -1,
		pageXOffset: 0,
		pageYOffset: 0,
		mouseX: 0,
		mouseY: 0,
		topNavHeight: 0,
		controlBarHeight: 0,
		footerHeight: 0,
		tooltipHtml: "", // HTML shown in a tooltip that follows the cursor.
		mouseUps: 0, // Counter of mouse up events at the document level.
		movingItem: null, // String indicating item(s) being moved via the context-menu method which doesn't require HTML5 drag and drop.
		externalChangesToVisibleEvents: 0, // A counter of external changes to visible events (incremented by FolderBrowser when it moves events into a different folder).
		projectFolderPathCache: {}
	},
	created()
	{
		window.addEventListener('resize', this.OnResize);
		window.addEventListener('scroll', this.OnScroll);
		document.addEventListener('mousemove', this.OnMouseMove);
		document.addEventListener('mouseup', this.OnMouseUp);
		this.OnResize();
	},
	mounted()
	{
	},
	beforeDestroy()
	{
		window.removeEventListener('resize', this.OnResize);
		window.removeEventListener('scroll', this.OnScroll);
		document.removeEventListener('mousemove', this.OnMouseMove);
		document.removeEventListener('mouseup', this.OnMouseUp);
	},
	computed:
	{
	},
	methods:
	{
		OnResize(event)
		{
			this.windowWidth = window.innerWidth;
			this.windowHeight = window.innerHeight;
			this.topNavHeight = MeasureHeightOfId("topNav");
			this.controlBarHeight = MeasureHeightOfId("controlBar");
			this.footerHeight = MeasureHeightOfId("appFooter");
		},
		OnScroll(event)
		{
			this.pageXOffset = window.pageXOffset;
			this.pageYOffset = window.pageYOffset;
		},
		OnMouseMove(event)
		{
			this.mouseX = event.pageX;
			this.mouseY = event.pageY;
		},
		OnMouseUp(event)
		{
			this.mouseUps++;
		},
		stopMovingItem()
		{
			if (this.movingItem)
			{
				this.movingItem = null;
				this.tooltipHtml = "";
			}
		},
		/**
		 * Pass the root folder into here each time you retrieve it.
		 * @param {String} projectName Project Name
		 * @param {any} folder Folder to traverse
		 */
		learnProjectFolderStructure(projectName, folder)
		{
			if (!this.projectFolderPathCache[projectName])
				Vue.set(this.projectFolderPathCache, projectName, {});
			Vue.set(this.projectFolderPathCache[projectName], folder.FolderId, folder.AbsolutePath);
			if (folder.Children)
				for (let i = 0; i < folder.Children.length; i++)
					this.learnProjectFolderStructure(projectName, folder.Children[i]);
		},
		/**
		 * This method will return the last known absolute folder path for the given Folder Id.
		 * @param {String} projectName Project Name
		 * @param {Number} folderId Folder ID to look up.
		 */
		getProjectFolderPathFromId(projectName, folderId)
		{
			if (this.projectFolderPathCache[projectName])
				return this.projectFolderPathCache[projectName][folderId];
			else
				return null;
		}
	}
});
function MeasureHeightOfId(id)
{
	let ele = document.getElementById(id);
	if (ele)
		return ele.offsetHeight;
	return 0;
}
window.appEventBus = EventBus; // Handle for debugging
export default EventBus;