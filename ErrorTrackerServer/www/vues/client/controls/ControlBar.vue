<template>
	<div id="controlBar" class="controlBar">
		<div class="currentLocation">
			<div v-if="!projectName">A project name was not specified in the URL.  <router-link :to="{ name: 'clientHome' }">Return Home</router-link></div>
			<template v-else>
				<router-link :to="{ name: 'clientHome', query: { p: projectName }}" :class="{ pathComponent: true, clickable: onFilters }">{{projectName}}</router-link>
				&gt;
				<router-link :to="{ name: 'clientFilters', query: { p: projectName }}" :class="{ pathComponent: true, clickable: !onFilters || filterId, filterListLink: true }">Filter List</router-link>
				<template v-if="filterId">
					&gt;
					<router-link :to="{ name: 'clientFilters', query: { p: projectName }, params: { filterId: filterId }}" class="pathComponent">Filter {{filterId}}</router-link>
					<a role="button" tabindex="0" v-if="dirty" @click="commitChanges" class="dirty pathComponent clickable">Commit Unsaved Changes</a>
				</template>
			</template>
		</div>
		<div v-if="!onFilters" title="Toggle event body position" class="svgButtonWrapper">
			<SvgButton :class="{ eventBodyBelow: true, isBelow: eventBodyBelow }"
					   :sprite="eventBodyBelow_sprite"
					   :size="35"
					   @click="eventBodyPositionToggle" />
		</div>
	</div>
</template>

<script>
	import EventBus from 'appRoot/scripts/EventBus';
	import svg1 from 'appRoot/images/sprite/view_column.svg';
	import svg2 from 'appRoot/images/sprite/view_compact.svg';
	import SvgButton from 'appRoot/vues/common/controls/SvgButton.vue';

	export default {
		components: { SvgButton },
		props:
		{
			projectName: {
				type: String,
				required: true
			},
			onFilters: {
				type: Boolean,
				default: false
			},
			filterId: {
				type: Number,
				default: 0
			},
			dirty: {
				type: Boolean,
				default: false
			}
		},
		created()
		{
		},
		mounted()
		{
			EventBus.OnResize();
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
			}
		},
		methods:
		{
			commitChanges()
			{
				this.$emit("commit");
			},
			eventBodyPositionToggle()
			{
				this.$store.commit("SetEventBodyBelow", !this.eventBodyBelow);
			}
		}
	}
</script>

<style scoped>
	.controlBar
	{
		position: sticky;
		top: 0px;
		display: flex;
		align-items: center;
		justify-content: space-between;
		height: 35px;
		border-bottom: 1px solid #545454;
		box-shadow: 0px 1px 5px rgba(0,0,0,0.5);
		background-color: #A8CBDB;
		padding: 2px 2px;
	}

	.currentLocation
	{
	}

	.pathComponent
	{
		font-size: 18px;
		padding: 2px;
		color: #000000;
		text-decoration: none;
		cursor: default;
		font-weight: bold;
	}

		.pathComponent.clickable
		{
			cursor: pointer;
			font-weight: normal;
			color: #0000ee;
		}

			.pathComponent.clickable:hover
			{
				background-color: rgba(255,255,255,0.2);
			}

	.dirty.clickable
	{
		background-color: #d40000;
		padding: 10px 12px;
		color: #FFFFFF;
		text-decoration: underline;
		margin-left: 4px;
	}

		.dirty.clickable:hover
		{
			background-color: #f12323;
		}

	.svgButtonWrapper
	{
		font-size: 0px;
	}

	.svgbtn
	{
		fill: #0000ee;
	}

	.eventBodyBelow.isBelow
	{
		transform: scaleY(-1);
	}
</style>