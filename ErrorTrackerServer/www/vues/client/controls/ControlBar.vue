<template>
	<div id="controlBar" class="controlBar">
		<div class="currentLocation">
			<div v-if="!projectName">A project name was not specified in the URL.  <router-link :to="{ name: 'clientHome' }">Return Home</router-link></div>
			<!-- TODO: Change the currentLocation to depict the folder path when not in the filter list.  Put the Filter List link somewhere to the right instead. -->
			<template v-else>
				<router-link :to="{ name: 'clientHome', query: { p: projectName }}" :class="{ pathComponent: true, clickable: projectNameClickable }">{{projectName}}</router-link>
				<template v-if="onAdvancedSearch">
					&gt; <b>Advanced Search</b>
				</template>
				<template v-if="onFilters">
					&gt;
					<router-link :to="{ name: 'clientFilters', query: { p: projectName }}" :class="{ pathComponent: true, clickable: !onFilters || filterId, filterListLink: true }">Filter List</router-link>
					<template v-if="filterId">
						&gt;
						<router-link :to="{ name: 'clientFilters', query: { p: projectName }, params: { filterId: filterId }}" class="pathComponent">Filter {{filterId}}</router-link>
						<a role="button" tabindex="0" v-if="dirty" @click="commitChanges" class="dirty pathComponent clickable">Commit Unsaved Changes</a>
					</template>
				</template>
				<template v-else>
					&gt;
					<code class="inline" v-if="folderPath">{{folderPath}}</code>
					<code class="inline" v-else>Folder {{selectedFolderId}} (path not cached yet)</code>
				</template>
			</template>
		</div>
		<router-link v-if="!onFilters" :to="{ name: 'clientFilters', query: { p: projectName }}" class="filterBtn">
			<vsvg sprite="filter_alt" class="filterIcon" />
			Edit Filters
		</router-link>
		<div v-if="!onFilters && !onAdvancedSearch" class="searchBar" title="Perform a &quot;Contains&quot; search on the Message, EventType, SubType, and all Tag values.">
			<input type="search" v-model="searchQuery" class="searchInput" placeholder="Search" @keypress.enter.prevent="doSearch" />
			<vsvg sprite="search" role="button" tabindex="0" @click="doSearch" @keypress.enter.prevent="doSearch" title="search" class="searchBtn" />
		</div>
		<router-link v-if="!onFilters && !onAdvancedSearch"
					 :to="{ name: 'advancedSearch', query: { p: projectName, f: selectedFolderId, matchAll: '1' }}"
					 class="advancedSearchBtn">
			<vsvg sprite="settings"
				  class="filterIcon"
				  title="advanced search" />
		</router-link>
		<SvgButton v-if="!onFilters && !onAdvancedSearch"
				   title="Toggle event body position"
				   :class="{ eventBodyBelow: true, isBelow: eventBodyBelow }"
				   :sprite="eventBodyBelow_sprite"
				   :size="35"
				   @click="eventBodyPositionToggle" />
	</div>
</template>

<script>
	import EventBus from 'appRoot/scripts/EventBus';
	import svg1 from 'appRoot/images/sprite/view_column.svg';
	import svg2 from 'appRoot/images/sprite/view_compact.svg';
	import svg3 from 'appRoot/images/sprite/search.svg';
	import svg4 from 'appRoot/images/sprite/settings.svg';
	import svg5 from 'appRoot/images/sprite/filter_alt.svg';
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
			},
			selectedFolderId: {
				type: Number,
				default: 0
			},
			onAdvancedSearch: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				searchQuery: ""
			};
		},
		created()
		{
		},
		mounted()
		{
			EventBus.OnResize();
		},
		updated()
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
			},
			folderPath()
			{
				return EventBus.getProjectFolderPathFromId(this.projectName, this.selectedFolderId);
			},
			projectNameClickable()
			{
				return this.onFilters || this.onAdvancedSearch;
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
			},
			doSearch()
			{
				let query = {
					p: this.projectName,
					f: this.selectedFolderId,
					q: this.searchQuery
				};
				this.$router.push({ name: "clientHome", query });
			}
		}
	}
</script>

<style scoped>
	.controlBar
	{
		position: sticky;
		top: 0px;
		z-index: 20;
		display: flex;
		flex-wrap: wrap;
		align-items: center;
		min-height: 40px;
		border-bottom: 1px solid #545454;
		box-shadow: 0px 1px 2px rgba(0,0,0,0.25);
		background-color: #A8CBDB;
		padding: 2px 2px;
		box-sizing: border-box;
	}

		.controlBar > *
		{
			flex: 0 1 auto;
		}

	.currentLocation
	{
		flex: 1 1 auto;
		padding: 0px 10px;
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

	.filterBtn,
	.advancedSearchBtn
	{
		display: flex;
		align-items: center;
		/*		color: #000000;*/
		color: #0000ee;
		text-decoration: none;
		font-weight: bold;
		border: 1px solid #000000;
		padding: 1px 5px 1px 1px;
		margin: 0px 5px;
	}

		.filterBtn:hover,
		.advancedSearchBtn:hover
		{
			background-color: rgba(0,0,0,0.05);
		}

		.filterBtn:active,
		.advancedSearchBtn:active
		{
			background-color: rgba(0,0,0,0.085);
		}

	.advancedSearchBtn
	{
		color: #000000;
		padding: 1px;
	}

	.filterIcon
	{
		width: 31px;
		height: 31px;
		fill: currentColor;
	}

	.searchBar
	{
		display: flex;
		align-items: stretch;
		height: 35px;
	}

	.searchInput
	{
		flex: 1 1 auto;
		min-width: 50px;
		max-width: 100%;
		width: 150px;
		font-size: 17px;
		padding: 1px 5px 1px 10px;
		border: 1px solid #AAAAAA;
		border-right: none;
		border-top-left-radius: 4px;
		border-bottom-left-radius: 4px;
		box-sizing: border-box;
	}

	.searchBtn
	{
		flex: 0 1 auto;
		width: 28px;
		padding: 0px 7px;
		border: 1px solid #AAAAAA;
		border-left-color: #DDDDDD;
		border-top-right-radius: 4px;
		border-bottom-right-radius: 4px;
		user-select: none;
		stroke: var(--search-button-color);
		cursor: pointer;
		background-color: #FFFFFF;
		fill: #272727;
	}

	.svgbtn
	{
		/*		color: #0000ee;*/
		fill: currentColor;
	}

	.advancedSearchBtn
	{
		flex: 0 1 auto;
		margin: 0px 5px;
	}

	.eventBodyBelow.isBelow
	{
		transform: scaleY(-1);
	}

	@media (min-width: 600px)
	{
		.searchInput
		{
			width: 100%;
		}
	}
</style>