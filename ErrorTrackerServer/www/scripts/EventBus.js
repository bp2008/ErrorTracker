import Vue from 'vue';
/////////////////////////////////////////
// READ THIS BEFORE IMPORTING ANYTHING //
/////////////////////////////////////////
// EventBus is a vue component that is created when this file is imported.
//
// Circular dependencies can create a complex situation where a dependency imported by EventBus.js is not loaded before the EventBus component is created.

// If you need to import something here, make sure that EventBus.js is imported very early in the application initialization.  A good place would be just after importing babel-polyfill (which should typically be the first thing imported).
/////////////////////////////////////////

import { MoveEventsMap } from 'appRoot/api/EventData';
import { MoveFolder } from 'appRoot/api/FolderData';
import { ProgressDialog } from 'appRoot/scripts/ModalDialog';

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
		externalChangesToFolders: 0, // A counter of external changes to folders (incremented by EventBus when undoing or redoing folder moves).
		projectFolderPathCache: {},
		undoStack: {}, // Stacks of operations which can be undone, keyed by projectName
		redoStack: {}, // Stacks of operations recently undone which can be redone, keyed by projectName.
		undoRedoLocked: false,
		eventSummaryMap: {} // A map of eventId to EventSummary
	},
	created()
	{
		let ss = sessionStorage.getItem("et_eb_undoStack");
		if (ss)
			this.undoStack = JSON.parse(ss);
		ss = sessionStorage.getItem("et_eb_redoStack");
		if (ss)
			this.redoStack = JSON.parse(ss);

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
			if (folderId < 0)
				return "🗄️ All Folders";
			else if (folderId === 0)
				return "/";
			else if (this.projectFolderPathCache[projectName])
				return this.projectFolderPathCache[projectName][folderId];
			else
				return null;
		},
		/**
		 * Send each event summary you retrieve to here.
		 * @param {Object} eventSummary An event summary.
		 */
		learnEventSummary(eventSummary)
		{
			Vue.set(this.eventSummaryMap, eventSummary.EventId, eventSummary);
		},
		/**
		 * Call when an operation is performed that can be undone.
		 * @param {String} projectName The name of the project.
		 * @param {Object} operation The operation which was performed.
		 */
		getEventSummary(eventId)
		{
			return this.eventSummaryMap[eventId];
		},
		/**
		 * Call when an operation is performed that can be undone.
		 * @param {String} projectName The name of the project.
		 * @param {Object} operation The operation which was performed.
		 */
		NotifyPerformedUndoableOperation(projectName, operation)
		{
			Vue.set(this.redoStack, projectName, []);
			if (!this.undoStack[projectName])
				Vue.set(this.undoStack, projectName, []);
			this.undoStack[projectName].push(operation);
			this.saveUndoRedoStacks();
		},
		saveUndoRedoStacks()
		{
			sessionStorage.setItem("et_eb_undoStack", JSON.stringify(this.undoStack));
			sessionStorage.setItem("et_eb_redoStack", JSON.stringify(this.redoStack));
		},
		/**
		 * Gets the next operation that could be undone.
		 * @param {String} projectName The name of the project.
		 */
		NextUndoOperation(projectName)
		{
			let stk = this.undoStack[projectName];
			return stk && stk.length > 0 ? stk[stk.length - 1] : null;
		},
		/**
		 * Gets the next operation that could be redone.
		 * @param {String} projectName The name of the project.
		 */
		NextRedoOperation(projectName)
		{
			let stk = this.redoStack[projectName];
			return stk && stk.length > 0 ? stk[stk.length - 1] : null;
		},
		/**
		 * Undoes the last operation on the undo stack and moves it to the redo stack.
		 * @param {String} projectName The name of the project.
		 */
		PerformUndo(projectName)
		{
			if (this.undoRedoLocked)
			{
				toaster.warning("An undo or redo operation is already in progress. Please try again later.");
				return;
			}
			let undo = this.undoStack[projectName];
			let redo = this.redoStack[projectName];
			if (!undo || !redo)
				return;
			if (undo.length > 0)
			{
				let item = undo.splice(undo.length - 1, 1)[0];
				redo.push(item);
				this.saveUndoRedoStacks();

				if (item.type === "MoveEvents")
				{
					// Undo Event Move
					// Original operation described by:
					// item.moves: [{ eventId, from, to }, ...]
					let map = {};
					for (let i = 0; i < item.moves.length; i++)
						map[item.moves[i].eventId] = item.moves[i].from;
					let progressDialog = ProgressDialog("Undoing " + item.description);
					this.undoRedoLocked = true;
					MoveEventsMap(projectName, map)
						.then(data =>
						{
							if (data.success)
							{
								for (let i = 0; i < item.moves.length; i++)
								{
									let eventSummary = this.getEventSummary(item.moves[i].eventId);
									if (eventSummary)
									{
										this.$emit("eventMoved", {
											event: eventSummary,
											from: item.moves[i].to,
											to: item.moves[i].from
										});
									}
									else
										console.log("Unable to emit eventMoved event because the event summary is not cached for event ID " + item.moves[i].eventId);
								}
								toaster.success("Undid " + item.description);
							}
							else
								toaster.error(data.error);
						})
						.catch(err =>
						{
							toaster.error(err.message);
						})
						.finally(() =>
						{
							this.externalChangesToVisibleEvents++;
							this.undoRedoLocked = false;
							progressDialog.close();
						});
				}
				else if (item.type === "MoveFolder")
				{
					// Undo Folder Move
					// Original operation described by:
					// item.from
					// item.to
					// item.folderId
					let progressDialog = ProgressDialog("Undoing " + item.description);
					this.undoRedoLocked = true;
					MoveFolder(projectName, item.folderId, item.from)
						.then(data =>
						{
							if (data.success)
							{
								toaster.success("Undid " + item.description);
							}
							else
								toaster.error(data.error);
						})
						.catch(err =>
						{
							toaster.error(err.message);
						})
						.finally(() =>
						{
							this.externalChangesToFolders++;
							this.undoRedoLocked = false;
							progressDialog.close();
						});
				}
				else
				{
					toaster.error('Unhandled item type on undo stack: "' + item.type + '"');
				}
			}
			else
			{
				toaster.warning("Nothing to undo");
			}
		},
		/**
		 * Redoes the last operation on the redo stack and moves it to the undo stack.
		 * @param {String} projectName The name of the project.
		 */
		PerformRedo(projectName)
		{
			if (this.undoRedoLocked)
			{
				toaster.warning("An undo or redo operation is already in progress. Please try again later.");
				return;
			}
			let undo = this.undoStack[projectName];
			let redo = this.redoStack[projectName];
			if (!undo || !redo)
				return;
			if (redo.length > 0)
			{
				let item = redo.splice(redo.length - 1, 1)[0];
				undo.push(item);
				this.saveUndoRedoStacks();

				if (item.type === "MoveEvents")
				{
					// Redo Event Move
					let map = {};
					for (let i = 0; i < item.moves.length; i++)
						map[item.moves[i].eventId] = item.moves[i].to;
					let progressDialog = ProgressDialog("Redoing " + item.description);
					this.undoRedoLocked = true;
					MoveEventsMap(projectName, map)
						.then(data =>
						{
							if (data.success)
							{
								for (let i = 0; i < item.moves.length; i++)
								{
									let eventSummary = this.getEventSummary(item.moves[i].eventId);
									if (eventSummary)
									{
										this.$emit("eventMoved", {
											event: eventSummary,
											from: item.moves[i].from,
											to: item.moves[i].to
										});
									}
									else
										console.log("Unable to emit eventMoved event because the event summary is not cached for event ID " + item.moves[i].eventId);
								}
								toaster.success("Redid " + item.description);
							}
							else
								toaster.error(data.error);
						})
						.catch(err =>
						{
							toaster.error(err.message);
						})
						.finally(() =>
						{
							this.externalChangesToVisibleEvents++;
							this.undoRedoLocked = false;
							progressDialog.close();
						});
				}
				else if (item.type === "MoveFolder")
				{
					// Redo Folder Move
					let progressDialog = ProgressDialog("Redoing " + item.description);
					this.undoRedoLocked = true;
					MoveFolder(projectName, item.folderId, item.to)
						.then(data =>
						{
							if (data.success)
							{
								toaster.success("Redid " + item.description);
							}
							else
								toaster.error(data.error);
						})
						.catch(err =>
						{
							toaster.error(err.message);
						})
						.finally(() =>
						{
							this.externalChangesToFolders++;
							this.undoRedoLocked = false;
							progressDialog.close();
						});
				}
				else
				{
					toaster.error('Unhandled item type on redo stack: "' + item.type + '"');
				}
			}
			else
			{
				toaster.warning("Nothing to redo");
			}
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