<template>
	<div class="eventBrowser">
		<div v-if="error" class="error">
			{{error}}
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadEvents" /></div>
		</div>
		<div v-else-if="events" class="eventListWrapper"
			 @contextmenu.stop.prevent="onContextmenu">
			<div class="eventListHeading">
				<div class="path">{{path}}</div>
				<div class="eventTotals"><a class="newEvents" v-if="newEvents > 0" title="click to scroll to top" role="button" tabindex="0" @click="scrollToTop" @keypress.enter.prevent="scrollToTop">^ {{newEvents}} new ^</a> {{selectedEventIdsArray.length}}/{{eventCount}}</div>
			</div>
			<RecycleScroller class="eventList"
							 :items="events"
							 :item-size="eventItemSize"
							 key-field="EventId"
							 v-slot="{ item }"
							 ref="eventScroller"
							 :emitUpdate="true"
							 @update="eventScrollerUpdated">
				<EventNode :projectName="projectName"
						   :event="item"
						   :selected="isEventSelected(item)"
						   :selectionState="selectionState"
						   :selectedEventIdsArray="selectedEventIdsArray"
						   :selectedEventIdsMap="selectedEventIdsMap"
						   @menu="onMenu"
						   @navUp="onNavUp"
						   @navDown="onNavDown" />
			</RecycleScroller>
			<div v-if="loading" class="loadingOverlay">
				<div class="loading"><ScaleLoader /> Updating…</div>
			</div>
		</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else>
			Events not found.
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadEvents" /></div>
		</div>
		<!-- Event Context Menu -->
		<vue-context ref="menu">
			<template slot-scope="event">
				<li v-show="event.data">
					<a role="button" @click.prevent="beginMoveEvent(event.data)">Move</a>
				</li>
				<li v-show="event.data">
					<a role="button" @click.prevent="changeEventColor(event.data)">Change Color</a>
				</li>
				<li v-show="event.data">
					<a role="button" @click.prevent="deleteEvent(event.data)">Delete</a>
				</li>
				<li v-show="event.data && event.data.Read">
					<a role="button" @click.prevent="markRead(event.data, false)">Mark as unread</a>
				</li>
				<li v-show="event.data && !event.data.Read">
					<a role="button" @click.prevent="markRead(event.data, true)">Mark as read</a>
				</li>
				<li v-if="nextUndo">
					<a role="button" @click.prevent="undo()">Undo {{nextUndo.description}}</a>
				</li>
				<li v-if="nextRedo">
					<a role="button" @click.prevent="redo()">Redo {{nextRedo.description}}</a>
				</li>
				<li v-if="!nextUndo && !nextRedo && !event.data">
					<span class="menuComment">no context menu items</span>
				</li>
			</template>
		</vue-context>
	</div>
</template>

<script>
	import { GetEvents, SetEventsColor, DeleteEvents, SetEventsReadState } from 'appRoot/api/EventData';
	import { VueContext } from 'vue-context';
	import EventNode from 'appRoot/vues/client/projectdisplay/event/EventNode.vue';
	import EventBus from 'appRoot/scripts/EventBus';
	import { ColorInputDialog } from 'appRoot/scripts/ModalDialog';
	import { ModalConfirmDialog } from '../../../../scripts/ModalDialog';
	import { SearchSimple, SearchAdvanced } from 'appRoot/api/SearchData';

	export default {
		components: { VueContext, EventNode },
		props:
		{
			projectName: { // pre-validated
				type: String,
				required: true
			},
			selectedFolderId: {
				type: Number,
				default: 0
			},
			selectedEventIds: {
				type: String,
				default: ""
			},
			searchArgs: null,
			openedEventId: {
				default: null
			},
			uniqueOnly: {
				type: Boolean,
				default: false
			}
		},
		created()
		{
			this.loadEvents();
		},
		mounted()
		{
			//let fakeEventInterval = setInterval(() =>
			//{
			//	if (this.$refs.eventScroller)
			//	{
			//		this.insertEvents([{ Color: "0000FF", Date: Date.now(), EventId: Date.now(), EventType: "Info", Message: "Dynamically-added fake event", SubType: "Dynamic Event" }]);
			//	}
			//	else
			//	{
			//		console.log("Clearing Interval " + fakeEventInterval);
			//		clearInterval(fakeEventInterval);
			//	}
			//}, 500);
			//console.log("Starting Interval " + fakeEventInterval);
		},
		beforeDestroy()
		{
			if (EventBus.movingItem && EventBus.movingItem.indexOf('e') === 0)
				EventBus.stopMovingItem();
		},
		data()
		{
			return {
				eventNodeComponent: EventNode,
				error: null,
				loading: false,
				events: null,
				eventItemSize: 37,
				newEvents: 0,
				selectionState: {
					lastSelectedEventId: null,
					getEventsBetween: this.getEventsBetween
				}
			};
		},
		computed:
		{
			selectedEventIdsArray()
			{
				let arr = [];
				if (this.selectedEventIds)
				{
					let splitted = this.selectedEventIds.split(',');
					let eMap = this.eventIdMap;
					for (let i = 0; i < splitted.length; i++)
					{
						let id = parseInt(splitted[i]);
						if (!isNaN(id) && eMap[id])
							arr.push(id);
					}
				}
				return arr;
			},
			selectedEventIdsMap()
			{
				let m = {};
				for (let i = 0; i < this.selectedEventIdsArray.length; i++)
					m[this.selectedEventIdsArray[i]] = true;
				return m;
			},
			eventIdMap()
			{
				let m = {};
				let evs = this.events;
				if (evs)
					for (let i = 0; i < evs.length; i++)
						m[evs[i].EventId] = evs[i];
				return m;
			},
			externalChangesToVisibleEvents()
			{
				return EventBus.externalChangesToVisibleEvents;
			},
			eventCount()
			{
				return this.events ? this.events.length : 0;
			},
			searchQuery()
			{
				return !this.searchArgs ? null : this.searchArgs.query;
			},
			searchMatchAll()
			{
				return !this.searchArgs ? null : this.searchArgs.matchAll;
			},
			searchConditionsStr()
			{
				return !this.searchArgs || !this.searchArgs.conditions ? null : JSON.stringify(this.searchArgs.conditions);
			},
			path()
			{
				return EventBus.getProjectFolderPathFromId(this.projectName, this.selectedFolderId);
			},
			nextUndo()
			{
				return EventBus.NextUndoOperation(this.projectName);
			},
			nextRedo()
			{
				return EventBus.NextRedoOperation(this.projectName);
			}
		},
		methods:
		{
			loadEvents()
			{
				this.loading = true;
				this.error = null;

				if (EventBus.movingItem && EventBus.movingItem.indexOf('e') === 0)
					EventBus.stopMovingItem();
				//var tomorrow = new Date();
				//tomorrow.setDate(tomorrow.getDate() + 1);

				//var yesterday = new Date();
				//yesterday.setDate(yesterday.getDate() - 1);

				let promise = null;
				if (this.searchQuery)
					promise = SearchSimple(this.projectName, this.selectedFolderId, this.searchQuery);
				else if (this.searchConditionsStr)
					promise = SearchAdvanced(this.projectName, this.selectedFolderId, this.searchMatchAll, this.searchArgs.conditions);
				else
					promise = GetEvents(this.projectName, this.selectedFolderId, 0, 0, this.uniqueOnly);
				this.handleEventListLoadPromise(promise);
			},
			handleEventListLoadPromise(promise)
			{
				if (!promise)
				{
					this.error = "Application Error. Event browser has invalid properties.";
					this.loading = false;
					return;
				}
				promise
					.then(data =>
					{
						if (data.success)
						{
							this.newEvents = 0;
							data.events.sort((a, b) =>
							{
								return b.Date - a.Date;
							});
							for (let i = 0; i < data.events.length; i++)
							{
								// Our infinite scroller component prevents us keeping references to the components, so here we define a function on the event objects that we can overwrite within EventNode.vue to provide access to the component.
								data.events[i].keyboardNav = () => { };
							}
							this.events = data.events;
							this.markOpenedEventAsRead();
						}
						else
						{
							this.error = data.error;
							this.events = null;
						}
					})
					.catch(err =>
					{
						this.error = err.message;
						this.events = null;
					})
					.finally(() =>
					{
						this.loading = false;
					});
			},
			isEventSelected(event)
			{
				if (!event)
					return false;
				return this.selectedEventIdsMap[event.EventId];
			},
			getEventsBetween(a, b)
			{
				let arr = [];
				let endOn = null;
				for (let i = 0; i < this.events.length; i++)
				{
					let e = this.events[i];
					if (arr.length === 0)
					{
						// We haven't found the first item yet
						if (e.EventId === a)
						{
							endOn = b;
							arr.push(e.EventId);
						}
						else if (e.EventId === b)
						{
							endOn = a;
							arr.push(e.EventId);
						}
					}
					else
					{
						// We found the first item already, so now we need to continue collecting events until we find the last item
						arr.push(e.EventId);
					}
					if (e.EventId === endOn)
						break; // Last item found
				}
				return arr;
			},
			onMenu({ e, event })
			{
				if (EventBus.movingItem)
					EventBus.stopMovingItem();
				else
					this.$refs.menu.open(e, event);
			},
			onContextmenu(e)
			{
				this.onMenu({ e: e, event: null });
			},
			beginMoveEvent(event)
			{
				let dragContextString = "e" + event.EventId;
				let movingThing = "1 event";
				let selectedEvents = this.selectedEventIdsArray;
				if (selectedEvents.indexOf(event.EventId) > -1)
				{
					dragContextString = "e" + selectedEvents.join(",e");
					movingThing = selectedEvents.length + " event" + (selectedEvents.length > 1 ? "s" : "");
				}
				EventBus.movingItem = dragContextString;
				EventBus.tooltipHtml = 'Carrying ' + movingThing + '.\n\nClick another folder to drop into.';
			},
			changeEventColor(event)
			{
				ColorInputDialog("Pick a Color", "Event Color", event.Color).then(data =>
				{
					if (data)
					{
						let selectedEvents = this.selectedEventIdsArray;
						if (selectedEvents.indexOf(event.EventId) === -1)
							selectedEvents = [event.EventId]; // The right-clicked event is not one of the selected events.

						let newColor = data.value;

						SetEventsColor(this.projectName, selectedEvents, newColor)
							.then(data =>
							{
								if (data.success)
								{
									// Build a map of EventId -> EventSummary for later use.
									let allEventsById = {};
									if (this.events)
										for (let i = 0; i < this.events.length; i++)
											allEventsById[this.events[i].EventId] = this.events[i];

									for (let i = 0; i < selectedEvents.length; i++)
									{
										// Inform listeners that this event's color has changed.
										EventBus.$emit("EventColorChange", { eventId: selectedEvents[i], color: newColor });

										// Update our EventSummary instance.
										let eventSummary = allEventsById[selectedEvents[i]];
										if (eventSummary)
											eventSummary.Color = newColor;
									}
									toaster.success("Color changed");
								}
								else
								{
									toaster.error(data.error);
								}
							})
							.catch(err =>
							{
								toaster.error(err);
							});
					}
				});
			},
			deleteEvent(event)
			{
				let selectedEvents = this.selectedEventIdsArray;
				if (selectedEvents.indexOf(event.EventId) === -1)
					selectedEvents = [event.EventId]; // The right-clicked event is not one of the selected events.
				ModalConfirmDialog("Do you want to delete " + selectedEvents.length + " event" + (selectedEvents.length == 1 ? "" : "s") + "?", "Confirm Delete").then(result =>
				{
					if (result)
					{
						DeleteEvents(this.projectName, selectedEvents)
							.then(data =>
							{
								if (data.success)
								{
									toaster.success("Events deleted");
									for (let i = 0; i < selectedEvents.length; i++)
									{
										let eventSummary = EventBus.getEventSummary(selectedEvents[i]);
										if (eventSummary)
											EventBus.$emit("eventDeleted", eventSummary);
										else
											console.log("Unable to emit eventDeleted event because the event summary is not cached for event ID " + selectedEvents[i]);
									}
									this.loadEvents();
								}
								else
									toaster.error(data.error);
							})
							.catch(err =>
							{
								toaster.error(data.error);
							});
					}
				});
			},
			markRead(event, read)
			{
				let selectedEvents = this.selectedEventIdsArray;
				if (selectedEvents.indexOf(event.EventId) === -1)
					selectedEvents = [event.EventId]; // The right-clicked event is not one of the selected events.

				SetEventsReadState(this.projectName, selectedEvents, !!read)
					.then(data =>
					{
						if (data.success)
						{
							if (this.events)
							{
								let selMap = {};
								for (let i = 0; i < selectedEvents.length; i++)
									selMap[selectedEvents[i]] = true;
								for (let i = 0; i < this.events.length; i++)
								{
									if (selMap[this.events[i].EventId])
									{
										let was = this.events[i].Read;
										this.events[i].Read = !!read;
										if (was !== this.events[i].Read)
											EventBus.$emit("eventReadStateChanged", this.events[i]);
									}
								}
							}
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(data.error);
					});
			},
			selectAll()
			{
				if (this.events)
				{
					let allEventIds = this.events.map(e => e.EventId);
					let query = Object.assign({}, this.$route.query);
					query.se = allEventIds.join(',');
					this.$router.replace({ name: this.$route.name, query });
				}
			},
			eventScrollerUpdated()
			{
				if (this.$refs.eventScroller && this.$refs.eventScroller.$el)
				{
					let eventsAboveScroller = Math.floor(this.$refs.eventScroller.$el.scrollTop / this.eventItemSize);
					if (this.newEvents > eventsAboveScroller)
						this.newEvents = eventsAboveScroller;
				}
			},
			insertEvents(eventsToInsert)
			{
				if (this.events)
				{
					this.events.splice(0, 0, { Color: "0000FF", Date: Date.now(), EventId: Date.now(), EventType: "Info", Message: "Dynamically-added fake event", SubType: "Dynamic Event" });
					this.$nextTick(() =>
					{
						this.newEvents += eventsToInsert.length;
						if (this.$refs.eventScroller && this.$refs.eventScroller.$el)
							this.$refs.eventScroller.$el.scrollTop += (this.eventItemSize * eventsToInsert.length);
					});
				}
			},
			scrollToTop()
			{
				if (this.$refs.eventScroller && this.$refs.eventScroller.$el)
				{
					this.$refs.eventScroller.$el.scrollTop = 0;
					this.newEvents = 0;
				}
			},
			markOpenedEventAsRead()
			{
				if (this.openedEventId && this.events)
					for (let i = 0; i < this.events.length; i++)
						if (this.events[i].EventId === this.openedEventId)
						{
							let was = this.events[i].Read;
							this.events[i].Read = true;
							if (!was)
								EventBus.$emit("eventReadStateChanged", this.events[i]);
						}
			},
			onNavUp({ domEvent, event })
			{
				this.onNav(domEvent, event, -1);
			},
			onNavDown({ domEvent, event })
			{
				this.onNav(domEvent, event, 1);
			},
			onNav(domEvent, event, offset)
			{
				if (this.events)
				{
					for (let i = 0; i < this.events.length; i++)
					{
						let ev = this.events[i];
						if (ev.EventId === event.EventId)
						{
							i += offset;
							if (i >= this.events.length)
								i = this.events.length - 1;
							if (i < 0)
								i = 0;
							ev = this.events[i];
							ev.keyboardNav(domEvent);
							return;
						}
					}
				}
			},
			undo()
			{
				EventBus.PerformUndo(this.projectName);
			},
			redo()
			{
				EventBus.PerformRedo(this.projectName);
			}
		},
		watch:
		{
			projectName()
			{
				this.loadEvents();
			},
			selectedFolderId()
			{
				this.loadEvents();
			},
			externalChangesToVisibleEvents()
			{
				this.loadEvents();
			},
			searchQuery()
			{
				this.loadEvents();
			},
			searchMatchAll()
			{
				this.loadEvents();
			},
			searchConditionsStr()
			{
				this.loadEvents();
			},
			uniqueOnly()
			{
				if (!this.searchQuery && !this.searchConditionsStr)
					this.loadEvents();
			},
			openedEventId()
			{
				this.markOpenedEventAsRead();
			}
		}
	}
</script>

<style scoped>
	.eventBrowser
	{
		position: relative;
		width: 100%;
		height: 100%;
	}

	.eventListWrapper
	{
		width: 100%;
		height: 100%;
		display: flex;
		flex-direction: column;
	}

	.eventListHeading
	{
		background-color: #EEEEEE;
		font-size: 14px;
		padding: 1px 2px 1px 3px;
		border-bottom: 1px solid #666666;
		display: flex;
		/*		flex-wrap: wrap;*/
		justify-content: space-between;
	}

		.eventListHeading .eventTotals
		{
			margin-left: 5px;
		}

		.eventListHeading .newEvents
		{
			font-weight: bold;
			animation: blinkyGreen 2s steps(1, start) infinite normal;
		}

	@keyframes blinkyGreen
	{
		0%
		{
			color: #000000;
		}

		50%
		{
			color: #00FF88;
			background-color: rgba(0,50,0,0.75);
		}
	}

	.eventList
	{
		overflow: auto;
		height: 100%;
	}

	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.loadingOverlay
	{
		position: absolute;
		top: 0px;
		left: 0px;
		width: 100%;
		height: 100%;
		background-color: rgba(0,0,0,0.25);
		z-index: 10;
		user-select: none;
		display: flex;
		align-items: center;
		justify-content: center;
	}

		.loadingOverlay .loading
		{
			margin-top: 0px;
			background-color: rgba(255,255,255,0.95);
			padding: 4px;
			border-radius: 4px;
		}

	.error
	{
		padding: 20px 10px;
		color: #FF0000;
		font-weight: bold;
		text-align: center;
	}

	.tryAgain
	{
		margin-top: 10px;
	}

	.menuComment
	{
		display: block;
		padding: .5rem 1.5rem;
		font-weight: 400;
		color: #999999;
		font-style: italic;
		text-decoration: none;
		white-space: nowrap;
		background-color: transparent;
		border: 0;
	}
</style>