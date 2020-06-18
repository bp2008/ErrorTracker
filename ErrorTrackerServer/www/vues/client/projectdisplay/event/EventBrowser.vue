<template>
	<div class="eventBrowser">
		<div v-if="error" class="error">
			{{error}}
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadEvents" /></div>
		</div>
		<div v-else-if="events" class="eventListWrapper">
			<!--<template>
				<EventNode v-for="e in events" :key="'event' + e.EventId"
						   :projectName="projectName"
						   :event="e"
						   :selected="isEventSelected(e)"
						   :selectionState="selectionState"
						   @menu="onMenu" />
			</template>-->
			<RecycleScroller class="eventList"
							 :items="events"
							 :item-size="37"
							 key-field="EventId"
							 v-slot="{ item }">
				<EventNode :projectName="projectName"
						   :event="item"
						   :selected="isEventSelected(item)"
						   :selectionState="selectionState"
						   @menu="onMenu" />
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
				<li>
					<a role="button" @click.prevent="beginMoveEvent(event.data)">Move</a>
				</li>
				<li>
					<a role="button" @click.prevent="changeEventColor(event.data)">Change Color</a>
				</li>
				<li>
					<a role="button" @click.prevent="deleteEvent(event.data)">Delete</a>
				</li>
			</template>
		</vue-context>
	</div>
</template>

<script>
	import { GetEventsInFolder, SetEventsColor, DeleteEvents } from 'appRoot/api/EventData';
	import { VueContext } from 'vue-context';
	import EventNode from 'appRoot/vues/client/projectdisplay/event/EventNode.vue';
	import EventBus from 'appRoot/scripts/EventBus';
	import { ColorInputDialog } from 'appRoot/scripts/ModalDialog';
	import { ModalConfirmDialog } from '../../../../scripts/ModalDialog';

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
			}
		},
		created()
		{
			this.loadEvents();
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
					for (let i = 0; i < splitted.length; i++)
					{
						let id = parseInt(splitted[i]);
						if (!isNaN(id))
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
			externalChangesToVisibleEvents()
			{
				return EventBus.externalChangesToVisibleEvents;
			}
		},
		methods:
		{
			loadEvents()
			{
				// TODO: Make reloading the event list not clear the event list, and preserve scroll position.
				this.loading = true;
				this.error = null;

				if (EventBus.movingItem && EventBus.movingItem.indexOf('e') === 0)
					EventBus.stopMovingItem();
				//var tomorrow = new Date();
				//tomorrow.setDate(tomorrow.getDate() + 1);

				//var yesterday = new Date();
				//yesterday.setDate(yesterday.getDate() - 1);

				GetEventsInFolder(this.projectName, this.selectedFolderId, 0, 0)
					.then(data =>
					{
						if (data.success)
						{
							data.events.sort((a, b) =>
							{
								return b.Date - a.Date;
							});
							this.events = data.events;
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
			beginMoveEvent(event)
			{
				let dragContextString = "e" + event.EventId;
				let movingThing = "1 event";
				let selectedEvents = this.$route.query.se ? this.$route.query.se.split(',') : [];
				if (selectedEvents.indexOf(event.EventId.toString()) > -1)
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
						let selectedEvents = this.$route.query.se ? this.$route.query.se.split(',').map(eidString => parseInt(eidString)) : [];
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
				let selectedEvents = this.$route.query.se ? this.$route.query.se.split(',').map(eidString => parseInt(eidString)) : [];
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
			selectAll()
			{
				if (this.events)
				{
					let allEventIds = this.events.map(e => e.EventId);
					let query = Object.assign({}, this.$route.query);
					query.se = allEventIds.join(',');
					this.$router.replace({ name: this.$route.name, query });
				}
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
		position: relative;
		width: 100%;
		height: 100%;
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
</style>