<template>
	<div class="eventDetails">
		<div class="buttonBar">
			<router-link :to="{ name: 'clientFilters', query: { p: projectName }}" title="Open filter list">
				<SvgButton sprite="filter_alt"
						   :size="38" />
			</router-link>
			<span title="Toggle event body position">
				<SvgButton :class="{ eventBodyBelow: true, isBelow: eventBodyBelow }"
						   :sprite="eventBodyBelow_sprite"
						   :size="38"
						   @click="eventBodyPositionToggle" />
			</span>
		</div>
		<div v-if="error" class="error">
			{{error}}
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadEvent" /></div>
		</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="event" class="eventBody">
			<div class="title" :style="titleBarStyle" v-text="event.Message" />
			<div class="subTitle">
				<span class="eventType">{{event.EventType}}:</span>
				<span class="subType" v-text="event.SubType" />
			</div>
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
						<tr class="tagRow" v-for="(tag, index) in event.Tags" :key="index">
							<td class="tagKey" v-text="tag.Key"></td>
							<td class="tagValue" v-text="tag.Value"></td>
						</tr>
					</tbody>
				</table>
			</div>
		</div>
		<div v-else class="noEvent">
			[no event selected]
		</div>
	</div>
</template>
<script>
	import { GetEvent } from 'appRoot/api/EventData';
	import svg1 from 'appRoot/images/sprite/view_column.svg';
	import svg2 from 'appRoot/images/sprite/view_compact.svg';
	import svg3 from 'appRoot/images/sprite/filter_alt.svg';
	import SvgButton from 'appRoot/vues/common/controls/SvgButton.vue';
	import { GetDateStr, GetReadableTextColorHexForBackgroundColorHex } from 'appRoot/scripts/Util';
	import EventBus from 'appRoot/scripts/EventBus';

	export default {
		components: { SvgButton },
		props:
		{
			projectName: { // pre-validated
				type: String,
				required: true
			},
			openedEventId: {
				default: null
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
				event: null
			};
		},
		computed:
		{
			eventBodyBelow()
			{
				return this.$store.state.eventBodyBelow;
			},
			eventBodyBelow_sprite()
			{
				return this.eventBodyBelow ? "view_compact" : "view_column";
			},
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
				this.error = null;
				this.loadingEventId = this.openedEventId;

				GetEvent(this.projectName, this.loadingEventId)
					.then(data =>
					{
						if (data.success)
						{
							data.ev.Tags.sort((a, b) =>
							{
								return a.Key.localeCompare(b.Key);
							});
							this.event = data.ev;
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
			filterClicked()
			{
				this.$router.push
			},
			eventBodyPositionToggle()
			{
				this.$store.commit("SetEventBodyBelow", !this.eventBodyBelow);
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

	.buttonBar
	{
		font-size: 0px;
		line-height: 0px;
		margin: 0px;
		padding: 0px;
		float: right;
		background-color: #FFFFFF;
	}

		.buttonBar::v-deep svg
		{
			fill: #0088FF;
		}

	.eventBodyBelow.isBelow
	{
		transform: scaleY(-1);
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
	}

	.tagKey,
	.tagValue
	{
		border-bottom: 1px solid #e4e4e4;
	}
</style>
