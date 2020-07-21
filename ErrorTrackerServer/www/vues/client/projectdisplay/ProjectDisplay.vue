<template>
	<div :class="{ projectDisplay: true, eventBodyBelow: eventBodyBelow }">
		<ControlBar :projectName="projectName" :selectedFolderId="selectedFolderId" />
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else class="body" :style="bodyStyle">
			<div class="folderBrowserContainer" :style="folderBrowserStyle">
				<FolderBrowser :projectName="projectName" :selectedFolderId="selectedFolderId" :searchArgs="searchArgs" />
			</div>
			<ResizeBar :min="50" :max="600" :start="fbStart" :default="175" @change="fbChange" />
			<div class="eventBrowserContainer" :style="eventBrowserStyle" @keydown="onEventBrowserKeydown" tabindex="0">
				<EventBrowser ref="eventBrowser"
							  :projectName="projectName"
							  :selectedFolderId="selectedFolderId"
							  :selectedEventIds="selectedEventIds"
							  :searchArgs="searchArgs" />
			</div>
			<ResizeBar v-if="!eventBodyBelow" :min="100" :max="800" :start="ebStart" :default="400" :offset="fbStart" @change="ebChange" />
			<div v-if="!eventBodyBelow" class="eventDetailsContainer">
				<EventDetails :projectName="projectName" :openedEventId="openedEventId" />
			</div>
		</div>
		<ResizeBar v-if="eventBodyBelow" :horizontal="true" :min="100" :max="1800" :start="tbStart" :default="400" @change="tbChange" />
		<div v-if="eventBodyBelow" class="eventDetailsContainer" :style="eventDetailsStyle">
			<EventDetails :projectName="projectName" :openedEventId="openedEventId" />
		</div>
	</div>
</template>

<script>
	import FolderBrowser from 'appRoot/vues/client/projectdisplay/folder/FolderBrowser.vue';
	import EventBrowser from 'appRoot/vues/client/projectdisplay/event/EventBrowser.vue';
	import EventDetails from 'appRoot/vues/client/projectdisplay/event/EventDetails.vue';
	import ResizeBar from 'appRoot/vues/client/projectdisplay/ResizeBar.vue';
	import ControlBar from 'appRoot/vues/client/controls/ControlBar.vue';
	import EventBus from 'appRoot/scripts/EventBus';

	export default {
		components: { ControlBar, FolderBrowser, EventBrowser, EventDetails, ResizeBar },
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
			openedEventId: {
				default: null
			},
			selectedEventIds: {
				type: String,
				default: ""
			},
			searchArgs: null,
		},
		data()
		{
			return {
				error: null,
				loading: false
			};
		},
		created()
		{
		},
		computed:
		{
			availableHeight()
			{
				let otherThingsHeight = EventBus.topNavHeight + EventBus.controlBarHeight + EventBus.footerHeight;
				return EventBus.windowHeight - otherThingsHeight;
			},
			bodyStyle()
			{
				if (this.eventBodyBelow)
					return { height: (this.tbStart - EventBus.topNavHeight - EventBus.controlBarHeight) + "px" };
				else
					return { height: this.availableHeight + "px" };
			},
			folderBrowserStyle()
			{
				return {
					width: this.$store.getters.folderBrowserSize(this.projectName) + "px"
				};
			},
			eventBrowserStyle()
			{
				if (this.eventBodyBelow)
					return {};
				else
					return { width: this.$store.getters.eventBrowserSize(this.projectName) + "px" };
			},
			eventDetailsStyle()
			{
				if (this.eventBodyBelow)
				{
					return { height: (this.availableHeight - (this.tbStart - EventBus.topNavHeight - EventBus.controlBarHeight)) + "px" };
				}
				else
					return {};
			},
			fbStart() // folder browser start
			{
				return this.$store.getters.folderBrowserSize(this.projectName);
			},
			ebStart() // event browser start
			{
				return this.$store.getters.eventBrowserSize(this.projectName);
			},
			tbStart() // top browsers start -- only for the layout where the event body is below the folder/event browsers
			{
				return this.$store.getters.topBrowsersSize(this.projectName);
			},
			eventBodyBelow()
			{
				return this.$store.state.eventBodyBelow;
			}
		},
		methods:
		{
			fbChange(size)
			{
				this.$store.commit("SetFolderBrowserSize", { projectName: this.projectName, size });
			},
			ebChange(size)
			{
				this.$store.commit("SetEventBrowserSize", { projectName: this.projectName, size });
			},
			tbChange(size)
			{
				this.$store.commit("SetTopBrowsersSize", { projectName: this.projectName, size });
			},
			onEventBrowserKeydown(e)
			{
				if (e.ctrlKey && e.keyCode == 65) // 'a'
				{
					if (this.$refs.eventBrowser)
					{
						this.$refs.eventBrowser.selectAll();
						e.preventDefault();
					}
				}
			}
		},
		watch:
		{
		}
	}
</script>

<style scoped>
	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
	}

	.heading
	{
		font-size: 20px;
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		max-width: 400px;
	}

	.body
	{
		display: flex;
		position: relative;
		width: 100%;
	}

	.folderBrowserContainer
	{
		flex: 0 0 auto;
		border-right: 1px solid #A9B7C9;
		background-color: #FAFAFA;
		overflow: auto;
		height: 100%;
		box-sizing: border-box;
	}

	.eventBrowserContainer
	{
		flex: 0 0 auto;
		border-right: 1px solid #A9B7C9;
		background-color: #FFFFFF;
		height: 100%;
		box-sizing: border-box;
	}

	.eventDetailsContainer
	{
		flex: 1 1 auto;
		background-color: #FFFFFF;
		overflow: auto;
		height: 100%;
		box-sizing: border-box;
		min-width: 100px;
	}

	.eventBodyBelow .body
	{
	}

	.eventBodyBelow .folderBrowserContainer
	{
		border-bottom: 1px solid black;
	}

	.eventBodyBelow .eventBrowserContainer
	{
		border-bottom: 1px solid black;
		flex: 1 1 auto;
		border-right: none;
	}

	.eventBodyBelow .eventDetailsContainer
	{
		width: 100%;
		height: auto;
	}
</style>