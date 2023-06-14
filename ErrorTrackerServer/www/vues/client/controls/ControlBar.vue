<template>
	<div id="controlBar" class="controlBar">
		<div class="currentLocation">
			<div v-if="!projectName">A project name was not specified in the URL.  <router-link :to="{ name: 'clientHome' }">Return Home</router-link></div>
			<template v-else>
				<router-link :to="{ name: 'clientHome', query: { p: projectName }}" :class="{ pathComponent: true, clickable: projectNameClickable }">{{projectName}}</router-link>
				<template v-if="onAdvancedSearch">
					&gt;
					<b>Advanced Search</b>
				</template>
				<template v-if="onFilters">
					&gt;
					<router-link :to="{ name: 'clientFilters', query: { p: projectName, q: routeSearchQuery ? routeSearchQuery : undefined, rx: routeRegexSearch ? undefined : '0' }}" :class="{ pathComponent: true, clickable: !onFilters || filterId, filterListLink: true }">Filter List</router-link>
					<template v-if="filterId">
						&gt;
						<router-link :to="{ name: 'clientFilters', query: { p: projectName }, params: { filterId: filterId }}" class="pathComponent">Filter {{filterId}}</router-link>
						<a role="button" tabindex="0" v-if="dirty" @click="commitChanges" class="dirty pathComponent clickable">Commit Unsaved Changes</a>
					</template>
				</template>
				<template v-else>
					&gt;
					<input type="button" :value="folderPath" @click="changeFolder" />
				</template>
			</template>
		</div>
		<router-link v-if="!onFilters" :to="{ name: 'clientFilters', query: { p: projectName }}" class="filterBtn">
			<vsvg sprite="filter_alt" class="filterIcon" />
			Edit Filters
		</router-link>
		<div v-if="!onAdvancedSearch" class="searchBar" :title="searchTooltip">
			<input type="search" v-model="searchQuery" class="searchInput" placeholder="Search" @keypress.enter.prevent="doSearch" />
			<vsvg sprite="search" role="button" tabindex="0" @click="doSearch" @keypress.enter.prevent="doSearch" title="search" class="searchBtn" />
			<label v-if="onFilters" class="regexCb"><input type="checkbox" v-model="regexSearch" /> Regex</label>
		</div>
		<router-link v-if="!onFilters && !onAdvancedSearch"
					 :to="routeToAdvancedSearch"
					 class="advancedSearchBtn"
					 title="advanced search">
			<vsvg sprite="search_adv2"
				  class="filterIcon" />
		</router-link>
		<router-link v-if="!onFilters && !onAdvancedSearch"
					 :to="routeToToggleUniqueOnly"
					 :class="{ uniqueOnlyBtn: true, active: uniqueOnly }"
					 :title="uniqueOnlyTooltip">
			<vsvg sprite="fingerprint"
				  class="filterIcon" />
		</router-link>
		<vsvg v-if="NotificationsAvailable"
			  role="button"
			  tabindex="0"
			  @click="toggleNotifications"
			  @keypress.enter.prevent="toggleNotifications"
			  :title="isRegisteredForPush ? 'notifications enabled' : 'notifications disabled'"
			  :sprite="isRegisteredForPush ? 'notifications_active' : 'notifications_off'"
			  :class="{ filterIcon: true, notificationsBtn: true, active: isRegisteredForPush }" />
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
	import svg4 from 'appRoot/images/sprite/search_adv2.svg';
	import svg5 from 'appRoot/images/sprite/filter_alt.svg';
	import svg6 from 'appRoot/images/sprite/fingerprint.svg';
	import svg7 from 'appRoot/images/sprite/notifications_active.svg';
	import svg8 from 'appRoot/images/sprite/notifications_off.svg';
	import SvgButton from 'appRoot/vues/common/controls/SvgButton.vue';
	import { SelectFolderDialog, ProgressDialog } from 'appRoot/scripts/ModalDialog';
	import { SetSelectedFolder } from 'appRoot/scripts/Util';
	import { IsRegisteredForPush, RegisterPushNotificationsForFolder, UnregisterPushNotificationsForFolder } from 'appRoot/scripts/PushHelper';

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
			},
			uniqueOnly: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				searchQuery: "",
				regexSearch: false,
				isRegisteredForPush: false
			};
		},
		created()
		{
			this.learnPushRegistration();
			this.searchQuery = this.routeSearchQuery;
			this.regexSearch = this.routeRegexSearch;
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
				let path = EventBus.getProjectFolderPathFromId(this.projectName, this.selectedFolderId);
				if (!path)
					path = "Folder " + this.selectedFolderId + " (click to load path)";
				return path;
			},
			projectNameClickable()
			{
				return this.onFilters || this.onAdvancedSearch;
			},
			routeToAdvancedSearch()
			{
				let query = {
					p: this.projectName,
					f: this.selectedFolderId,
					matchAll: '1'
				};
				if (this.uniqueOnly)
					query.uo = "1";
				return { name: 'advancedSearch', query };
			},
			routeToToggleUniqueOnly()
			{
				let query = Object.assign({}, this.$route.query);
				query.uo = this.uniqueOnly ? "0" : "1";
				return { name: this.$route.name, query };
			},
			uniqueOnlyTooltip()
			{
				if (this.uniqueOnly)
					return "Showing only the newest event in each matching set.";
				else
					return "Showing all events in each matching set.";
			},
			NotificationsAvailable()
			{
				return EventBus.pushNotificationsAvailable;
			},
			NotificationPermission()
			{
				return EventBus.notificationPermission;
			},
			pushSubscription()
			{
				return EventBus.pushSubscription;
			},
			routeSearchQuery()
			{
				return this.$route.query.q ? this.$route.query.q : "";
			},
			routeRegexSearch()
			{
				return this.$route.query.rx !== "0";
			},
			searchTooltip()
			{
				if (this.onFilters)
					return 'Perform a "Regex" or "SQL Full Text" Search \n'
						+ 'on the filter name, conditions, and actions.\n\n'
						+ 'When viewing a filter, matches will be \n'
						+ 'highlighted, but this clientside matching is \n'
						+ 'different from the search engine in the \n'
						+ 'database server.';
				else
					return 'Perform a "Contains" search on the Message, EventType, SubType, and all Tag values.';
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
				if (this.onFilters)
				{
					let query = {
						p: this.projectName,
						q: this.searchQuery,
						rx: this.regexSearch ? undefined : '0'
					};
					let params;
					if (this.filterId)
						params = { filterId: this.filterId };
					this.$router.push({ name: "clientFilters", query, params });
				}
				else
				{
					let query = {
						p: this.projectName,
						f: this.selectedFolderId,
						q: this.searchQuery
					};
					if (this.uniqueOnly)
						query.uo = "1";
					this.$router.push({ name: "clientHome", query });
				}
			},
			changeFolder()
			{
				SelectFolderDialog(this.projectName, this.selectedFolderId, true)
					.then(folder =>
					{
						if (folder)
							SetSelectedFolder(this, false, folder.FolderId);
					});
			},
			toggleNotifications()
			{
				if (this.notificationsToggling)
					return;
				let progressDialog;
				let promise;
				if (this.isRegisteredForPush)
				{
					promise = UnregisterPushNotificationsForFolder(this.projectName, this.selectedFolderId);
					progressDialog = ProgressDialog("Unregistering");
				}
				else
				{
					promise = RegisterPushNotificationsForFolder(this.projectName, this.selectedFolderId);
					progressDialog = ProgressDialog("Registering");
				}
				this.notificationsToggling = true;
				promise
					.then(() =>
					{
						this.notificationsToggling = false;
						progressDialog.close();
						this.learnPushRegistration();
					})
					.catch(err =>
					{
						this.notificationsToggling = false;
						progressDialog.close();
						toaster.error(err);
						this.learnPushRegistration();
					});
			},
			learnPushRegistration()
			{
				// unset the flag, then asynchronously ask if we're registered to the current project and folder.
				this.isRegisteredForPush = false;

				let projectName = this.projectName;
				let folderId = this.selectedFolderId;
				IsRegisteredForPush(projectName, folderId)
					.then(isRegistered =>
					{
						if (this.projectName === projectName && this.selectedFolderId === folderId)
						{
							this.isRegisteredForPush = isRegistered;
						}
					});
			}
		},
		watch:
		{
			projectName()
			{
				this.learnPushRegistration();
			},
			selectedFolderId()
			{
				this.learnPushRegistration();
			},
			pushSubscription()
			{
				this.learnPushRegistration();
			},
			routeSearchQuery()
			{
				this.searchQuery = this.routeSearchQuery;
			},
			routeRegexSearch()
			{
				this.regexSearch = this.routeRegexSearch;
			},
			regexSearch()
			{
				if (this.regexSearch !== this.routeRegexSearch)
					this.doSearch();
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

	.eventBodyBelow
	{
		margin-left: 3px;
	}

	.filterBtn,
	.advancedSearchBtn,
	.uniqueOnlyBtn,
	.notificationsBtn
	{
		display: flex;
		align-items: center;
		/*		color: #000000;*/
		color: #0000ee;
		text-decoration: none;
		font-weight: bold;
		border: 1px solid #000000;
		padding: 1px 5px 1px 1px;
		margin: 0px 2px 0px 3px;
	}

	.advancedSearchBtn
	{
		margin-left: 5px;
		color: #000000;
		padding: 1px;
	}

	.uniqueOnlyBtn
	{
		color: rgba(0,0,0,0.15);
		padding: 1px;
	}

		.uniqueOnlyBtn.active,
		.notificationsBtn.active
		{
			color: #0000FF;
			background-color: rgba(255,255,255,.75);
		}

		.filterBtn:hover,
		.advancedSearchBtn:hover,
		.uniqueOnlyBtn:hover,
		.notificationsBtn:hover
		{
			background-color: rgba(0,0,0,0.05);
		}

		.filterBtn:active,
		.advancedSearchBtn:active,
		.uniqueOnlyBtn:active,
		.notificationsBtn:active
		{
			background-color: rgba(0,0,0,0.085);
		}

	.notificationsBtn
	{
		cursor: pointer;
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
	
	.regexCb
	{
		flex: 1 0 auto;
		line-height: 35px;
		padding: 0px 7px 0px 2px;
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