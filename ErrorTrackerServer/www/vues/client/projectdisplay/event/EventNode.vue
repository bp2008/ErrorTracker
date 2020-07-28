<template>
	<a :href="nodeLink"
	   @click="nodeClick"
	   :class="{ eventNode: true, selected: selected, read: event.Read, unread: !event.Read }"
	   :style="nodeStyle"
	   @contextmenu.stop.prevent="onContextmenu"
	   :draggable="!event ? 'false' : 'true'"
	   @dragstart="dragStart"
	   @keydown.up.prevent="upPressed"
	   @keydown.down.prevent="downPressed"
	   ref="link">
		<div class="firstLine">
			<span class="types">
				<span class="eventType">{{event.EventType}}</span>:
				<span class="subType">{{event.SubType}}</span>
			</span>
			<span class="date">{{eventDateFormat(new Date(event.Date))}}</span>
		</div>
		<div class="secondLine">{{event.Message}}</div>
		<div class="json" v-text="JSON.stringify(event,0,2)"></div>
	</a>
</template>
<script>
	import { GetDateStr, GetTimeStr, CopyArray } from 'appRoot/scripts/Util';

	export default {
		components: {},
		props:
		{
			projectName: {
				type: String,
				required: true
			},
			event: {
				type: Object,
				required: true
			},
			selected: {
				type: Boolean,
				default: false
			},
			selectionState: {
				Object,
				required: true
			},
			selectedEventIdsArray: {
				Object,
				required: true
			},
			selectedEventIdsMap: {
				Object,
				required: true
			}
		},
		data()
		{
			return {
			};
		},
		created()
		{
			this.bindKeyboardNav();
		},
		computed:
		{
			nodeLink()
			{
				let query = Object.assign({}, this.$route.query);
				query.e = this.event.EventId;
				let r = this.$router.resolve({ name: this.$route.name, query });
				return r.href;
			},
			nodeStyle()
			{
				return { borderLeftColor: "#" + this.event.Color };
			}
		},
		methods:
		{
			bindKeyboardNav()
			{
				if (this.event)
					this.event.keyboardNav = this.nodeClick; // keyboardNav is called by EventBrowser.vue
			},
			eventDateFormat(date)
			{
				if (typeof window.overrideEventNodeDateFormat === "function")
					return window.overrideEventNodeDateFormat(date);
				let now = new Date();
				if (now.getFullYear() === date.getFullYear() && now.getMonth() === date.getMonth() && now.getDate() === date.getDate())
					return GetTimeStr(date, false, false);
				else
					return GetDateStr(date, false);
			},
			nodeClick(e)
			{
				if (this.$refs.link)
					this.$refs.link.focus();

				let query = Object.assign({}, this.$route.query);

				let selectedEvents = CopyArray(this.selectedEventIdsArray);
				let selectedEventsMap = {};

				for (let i = 0; i < selectedEvents.length; i++)
					selectedEventsMap[selectedEvents[i]] = true;

				if (!this.selectionState.lastSelectedEventId && query.e)
					this.selectionState.lastSelectedEventId = parseInt(query.e);

				if (!this.selectionState.lastSelectedEventId && selectedEvents.length > 0)
					this.selectionState.lastSelectedEventId = selectedEvents[0];

				if ((e.shiftKey && this.selectionState.lastSelectedEventId) || e.ctrlKey)
				{
					if (e.shiftKey && this.selectionState.lastSelectedEventId)
					{
						// Multi-select add-range
						let range = this.selectionState.getEventsBetween(this.selectionState.lastSelectedEventId, this.event.EventId);

						if (!e.ctrlKey)
						{
							selectedEvents = [];
							selectedEventsMap = {};
						}

						if (range)
						{
							for (let i = 0; i < range.length; i++)
							{
								if (!selectedEventsMap[range[i]])
								{
									selectedEvents.push(range[i]);
									selectedEventsMap[range[i]] = true;
								}
							}
						}

						if (e.ctrlKey)
							this.selectionState.lastSelectedEventId = this.event.EventId;

						query.se = selectedEvents.join(',');
						this.$router.replace({ name: this.$route.name, query }).catch(() => { });
					}
					else if (e.ctrlKey)
					{
						// Multi-select toggle item
						this.selectionState.lastSelectedEventId = this.event.EventId;
						if (selectedEventsMap[this.event.EventId])
						{
							let idx = selectedEvents.indexOf(this.event.EventId);
							if (idx > -1)
								selectedEvents.splice(idx, 1);
							selectedEventsMap[this.event.EventId] = false;
						}
						else
						{
							selectedEvents.push(this.event.EventId);
							selectedEventsMap[this.event.EventId] = true;
						}
					}
					query.se = selectedEvents.join(',');
					this.$router.replace({ name: this.$route.name, query }).catch(() => { });
				}
				else
				{
					this.selectionState.lastSelectedEventId = this.event.EventId;
					query.e = this.event.EventId;
					query.se = this.event.EventId.toString();
					this.$router.replace({ name: this.$route.name, query }).catch(() => { });
				}

				e.preventDefault();
				return false;
			},
			onContextmenu(e)
			{
				this.$emit("menu", { e, event: this.event });
			},
			dragStart(e)
			{
				let dragContextString = "e" + this.event.EventId;
				if (this.selectedEventIdsMap[this.event.EventId])
					dragContextString = "e" + this.selectedEventIdsArray.join(",e");
				e.dataTransfer.setData("etrk_drag", dragContextString);
				e.dataTransfer.dropEffect = "move";
			},
			upPressed(e)
			{
				this.$emit("navUp", { domEvent: e, event: this.event });
			},
			downPressed(e)
			{
				this.$emit("navDown", { domEvent: e, event: this.event });
			}
		},
		watch:
		{
			event()
			{
				this.bindKeyboardNav();
			}
		}
	}
</script>
<style scoped>
	.eventNode
	{
		display: block;
		color: #000000;
		text-decoration: none;
		width: 100%;
		height: 37px;
		overflow: hidden;
		padding: 2px 3px 2px 5px;
		box-sizing: border-box;
		border-bottom: 1px solid #a7a7a7;
		border-left: 8px solid transparent;
	}

	.firstLine
	{
		width: 100%;
		display: flex;
		flex-wrap: nowrap;
		justify-content: space-between;
		overflow: hidden;
	}

	.types
	{
		font-size: 14px;
		flex: 1 1 auto;
	}

	.subType
	{
	}

	.date
	{
		margin-left: 5px;
		font-size: 14px;
		/*font-weight: bold;*/
		flex: 0 0 auto;
	}

	.secondLine
	{
		margin-top: 2px;
		font-size: 12px;
		color: #888888;
	}

	.types,
	.secondLine
	{
		overflow: hidden;
		white-space: nowrap;
		text-overflow: ellipsis;
	}

	.json
	{
		white-space: pre-wrap;
		display: none;
	}

	.eventNode.unread .firstLine
	{
		font-weight: bold;
	}

	.eventNode.read
	{
		background-color: #F5F7F7;
		color: #676767;
	}

	.eventNode:hover
	{
		background-color: #E5F3FF;
	}

	.eventNode:focus
	{
		outline: 2px solid #7BC3FF;
		border-bottom-color: transparent;
	}

	.eventNode.selected
	{
		background-color: #CDE8FF;
	}
</style>
