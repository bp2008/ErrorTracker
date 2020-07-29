<template>
	<div class="eventDetails">
		<div v-if="error" class="error">
			{{error}}
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadEvent" /></div>
		</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="event" class="eventBody">
			<div class="title" :style="titleBarStyle" v-text="event.Message" :title="'Event color: #' + event.Color" />
			<div class="subTitle" :title="'EventType: ' + event.EventType + ', SubType: ' + event.SubType">
				<span class="eventType">{{event.EventType}}:</span>
				<span class="subType" v-text="event.SubType" />
			</div>
			<div><b>Folder:</b> <router-link :to="routeToFolder">{{folderPath}}</router-link></div>
			<div v-if="routeToMatchingEvents"><router-link :to="routeToMatchingEvents">{{event.MatchingEvents}} events like this</router-link></div>
			<div class="date">
				<!--<span class="dateHeading">Date</span>:-->
				<span class="dateValue" v-text="eventDateFormat(new Date(event.Date))" />
			</div>
			<div class="messageContainer">
				<div class="messageHeading">Message</div>
				<div class="messageBody" v-text="event.Message" />
			</div>
			<div class="tagContainer">
				<table class="tagTable">
					<tbody>
						<tr class="tagRow" v-for="(tag, index) in event.TagList" :key="index">
							<td class="tagKey" v-text="tag.Key" @contextmenu.stop.prevent="onTagKeyContextMenu($event, tag)"></td>
							<td class="tagValue" v-text="tag.Value"></td>
						</tr>
					</tbody>
				</table>
			</div>
		</div>
		<div v-else class="noEvent">
			[no event selected]
		</div>
		<!-- Event Context Menu -->
		<vue-context ref="tagKeyMenu">
			<template slot-scope="tagName">
				<li if="tagName.data">
					<a role="button" @click.prevent="setEventListCustomTagKey(tagName.data)">Show &quot;{{tagName.data}}&quot; in Event List</a>
				</li>
				<li v-if="eventListCustomTagKey">
					<a role="button" @click.prevent="setEventListCustomTagKey(null)">Remove &quot;{{eventListCustomTagKey}}&quot; from Event List</a>
				</li>
			</template>
		</vue-context>
	</div>
</template>
<script>
	import { GetEvent, SetEventListCustomTagKey } from 'appRoot/api/EventData';
	import { GetDateStr, GetReadableTextColorHexForBackgroundColorHex } from 'appRoot/scripts/Util';
	import EventBus from 'appRoot/scripts/EventBus';
	import { VueContext } from 'vue-context';

	export default {
		components: { VueContext },
		props:
		{
			projectName: { // pre-validated
				type: String,
				required: true
			},
			openedEventId: {
				default: null
			},
			selectedFolderId: {
				type: Number,
				default: 0
			},
			uniqueOnly: {
				type: Boolean,
				default: false
			}
		},
		created()
		{
			EventBus.$on("EventColorChange", this.onEventColorChanged);
			this.loadEvent();
		},
		beforeDestroy()
		{
			EventBus.$off("EventColorChange", this.onEventColorChanged);
		},
		data()
		{
			return {
				error: null,
				loading: false,
				loadingEventId: null,
				event: null,
				eventListCustomTagKey: null
			};
		},
		computed:
		{
			titleBarStyle()
			{
				if (this.event)
				{
					return {
						backgroundColor: "#" + this.event.Color,
						color: "#" + GetReadableTextColorHexForBackgroundColorHex(this.event.Color, "000000", "FFFFFF")
					};
				}
				return {};
			},
			routeToMatchingEvents()
			{
				if (this.event && this.event.MatchingEvents > 1)
				{
					// Build advanced search query that will find matching events using the same logic as Event.ComputeHash().
					let conditions = [
						{ "Enabled": true, "TagKey": "EventType", "Operator": "Equals", "Query": this.event.EventType },
						{ "Enabled": true, "TagKey": "SubType", "Operator": "Equals", "Query": this.event.SubType }
					];
					let messageChars = 250;
					if (this.event.Message.length <= messageChars)
						conditions.push({ "Enabled": true, "TagKey": "Message", "Operator": "Equals", "Query": this.event.Message });
					else
						conditions.push({ "Enabled": true, "TagKey": "Message", "Operator": "StartsWith", "Query": this.event.Message.substr(0, messageChars) });

					let query = {
						p: this.projectName
						, f: -1
						, matchAll: "1"
						, scon: JSON.stringify(conditions)
					};
					if (this.uniqueOnly)
						query.uo = "1";

					return { name: this.$route.name, query };
				}
				else
					return null;
			},
			folderPath()
			{
				if (this.event)
				{
					let path = EventBus.getProjectFolderPathFromId(this.projectName, this.event.FolderId);
					if (path === "/")
						path = this.projectName + "/";
					return path;
				}
				return null;
			},
			routeToFolder()
			{
				let query = Object.assign({}, this.$route.query);
				query.f = this.event.FolderId;
				return { name: this.$route.name, query };
			}
		},
		methods:
		{
			loadEvent()
			{
				if (this.loading)
					return;
				if (!this.openedEventId)
				{
					this.event = null;
					return;
				}
				if (this.openedEventId === this.loadingEventId)
					return;
				this.loading = true;
				this.event = null;
				this.eventListCustomTagKey = null;
				this.error = null;
				this.loadingEventId = this.openedEventId;

				GetEvent(this.projectName, this.loadingEventId)
					.then(data =>
					{
						if (data.success)
						{
							let tagList = [];
							for (let key in data.ev.Tags)
								if (data.ev.Tags.hasOwnProperty(key))
									tagList.push(data.ev.Tags[key]);

							tagList.sort((a, b) =>
							{
								return a.Key.localeCompare(b.Key);
							});
							data.ev.TagList = tagList;

							this.event = data.ev;
							this.eventListCustomTagKey = data.eventListCustomTagKey;
						}
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.error = err.message;
					})
					.finally(() =>
					{
						this.loading = false;
						if (this.loadingEventId !== this.openedEventId)
							this.loadEvent();
						else
							this.loadingEventId = null;
					});
			},
			onEventColorChanged({ eventId, color })
			{
				if (this.event && this.event.EventId === eventId)
					this.event.Color = color;
			},
			eventDateFormat(date)
			{
				if (typeof window.overrideEventDetailsDateFormat === "function")
					return window.overrideEventDetailsDateFormat(date);
				return GetDateStr(date, false);
			},
			onTagKeyContextMenu(e, tag)
			{
				this.$refs.tagKeyMenu.open(e, tag.Key);
			},
			setEventListCustomTagKey(tagKey)
			{
				this.eventListCustomTagKey = tagKey;
				SetEventListCustomTagKey(this.projectName, tagKey)
					.then(data =>
					{
						if (data.success)
						{
							toaster.success("Custom tag key changed.");
							EventBus.externalChangesToVisibleEvents++;
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err.message);
					});
			}
		},
		watch:
		{
			projectName()
			{
				this.loadEvent();
			},
			openedEventId()
			{
				this.loadEvent();
			}
		}
	}
</script>
<style scoped>
	.eventDetails
	{
		width: 100%;
		box-sizing: border-box;
	}

	.loading
	{
		padding-top: 80px;
		text-align: center;
		animation: fadeIn linear 5s;
	}

	@keyframes fadeIn
	{
		0%
		{
			opacity: 0;
		}

		15%
		{
			opacity: 0;
		}

		100%
		{
			opacity: 1;
		}
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
		text-align: center;
		margin-top: 20px;
	}

	.tryAgain
	{
		margin-top: 10px;
	}

	.noEvent
	{
		color: #777777;
		text-align: center;
		padding-top: 20px;
	}

	.eventBody > *
	{
		padding: 7px 10px;
		border-bottom: 1px solid #e4e4e4;
		font-size: 16px;
	}

	.title
	{
		font-size: 18px;
		font-weight: bold;
		overflow: hidden;
		white-space: nowrap;
		text-overflow: ellipsis;
		padding: 10px 10px 7px 10px;
		background-color: #F6F6F6;
	}

	.colorBorder
	{
		width: 1px;
		background-color: #000000;
	}

	.subTitle
	{
		font-size: 16px;
	}

	.eventType,
	.dateHeading,
	.dateValue
	{
		font-weight: bold;
	}

	.messageContainer
	{
	}

	.messageHeading
	{
		font-weight: bold;
		color: #777777;
		text-transform: uppercase;
		padding: 10px 0px 10px 0px;
	}

	.messageBody
	{
		white-space: pre-wrap;
		padding-bottom: 12px;
	}

	.tagContainer
	{
		display: flex;
		padding: 0px;
	}

	.tagTable
	{
		border-collapse: collapse;
		width: 100%;
	}

	.tagKey
	{
		width: 150px;
		flex: 0 0 auto;
		font-weight: bold;
		color: #777777;
		padding: 7px 5px 7px 10px;
	}

	.tagValue
	{
		flex: 1 1 auto;
		padding: 7px 10px 7px 5px;
		white-space: pre-wrap;
	}

	.tagKey,
	.tagValue
	{
		border-bottom: 1px solid #e4e4e4;
	}
</style>
