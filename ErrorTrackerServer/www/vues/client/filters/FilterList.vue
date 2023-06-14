<template>
	<div>
		<ControlBar :projectName="projectName" :onFilters="true" />
		<div class="pageRoot">
			<div v-if="error" class="error">
				{{error}}
				<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilterSummaries" /></div>
			</div>
			<div v-else-if="loading || filterSummaries">
				<div class="heading">Filter List</div>
				<div v-if="searchQuery" class="filterSearchDetailsContainer"><b>Filtered by <span v-if="regexSearch" title="Regular Expression">regex</span><span v-else title="SQL Full Text Search">SQL FTS</span>:</b> {{searchQuery}} <input type="button" value="Clear" @click="clearSearchQuery" /></div>
				<div class="filterSummaryList" v-if="filterSummaries && filterSummaries.length">
					<draggable v-model="filterSummaries" @start="startDragging" @end="endDragging" :move="moveDragging">
						<router-link v-for="f in filterSummaries"
									 :key="f.filter.FilterId"
									 class="filterNode"
									 :to="makeFilterDetailsLink(f.filter)">
							<span class="filterName" v-text="f.filter.Name" />
							<span class="filterMeta">[{{f.NumConditions}} Conditions, {{f.NumActions}} Actions]</span>
							<span class="filterMeta filterEnabled" v-if="f.filter.Enabled">[Enabled]</span>
							<span class="filterMeta filterDisabled" v-if="!f.filter.Enabled">[Disabled]</span>
						</router-link>
					</draggable>
				</div>
				<div v-if="filterSummaries && filterSummaries.length === 0">This project does not have any filters yet.</div>
				<div class="buttonBar">
					<input type="button" value="New Filter" @click="newFilter" />
					<input type="button" value="Run enabled filters against all events" @click="runFilters" />
				</div>
				<div v-if="loading" class="loadingOverlay">
					<div class="loading"><ScaleLoader /> Loading…</div>
				</div>
			</div>
			<div v-else>
				Failed to load filter list for project: {{projectName}}
				<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilterSummaries" /></div>
			</div>
		</div>
	</div>
</template>

<script>
	import { GetAllFilters, AddFilter, ReorderFilters, RunEnabledFiltersAgainstAllEvents } from 'appRoot/api/FilterData';
	import { TextInputDialog, ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';
	import ControlBar from 'appRoot/vues/client/controls/ControlBar.vue';
	import draggable from 'vuedraggable'

	export default {
		components: { ControlBar, draggable },
		props:
		{
			projectName: {
				type: String,
				required: true
			},
			searchQuery: {
				type: String,
				default: ""
			},
			regexSearch: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				filterSummaries: null,
				filterOrderJson: null // Remembered the last filter order so we don't request a change unnecessarily
			};
		},
		created()
		{
			this.loadFilterSummaries();
		},
		computed:
		{
		},
		methods:
		{
			loadFilterSummaries()
			{
				this.loading = true;
				this.error = null;

				GetAllFilters(this.projectName, this.searchQuery, this.regexSearch)
					.then(data =>
					{
						if (data.success)
						{
							this.filterSummaries = data.filters;
							this.filterOrderSaved = JSON.stringify(this.getFilterOrder());
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
					});
			},
			makeFilterDetailsLink(f)
			{
				return {
					name: "clientFilters",
					query: { p: this.projectName, q: this.searchQuery ? this.searchQuery : undefined, rx: this.regexSearch ? undefined : '0' },
					params: { filterId: f.FilterId }
				};
			},
			handleAsyncFilterOp(promise)
			{
				this.loading = true;
				promise
					.then(data =>
					{
						if (data.success)
							this.loadFilterSummaries();
						else
						{
							toaster.error(data.error);
							this.loading = false;
						}
					})
					.catch(err =>
					{
						toaster.error(err);
						this.loading = false;
					});
			},
			newFilter()
			{
				TextInputDialog("New Filter", "Filter Name:", "Name")
					.then(data =>
					{
						if (data)
						{
							this.handleAsyncFilterOp(AddFilter(this.projectName, data.value));
						}
					});
			},
			startDragging(e)
			{
				if (this.searchQuery)
					toaster.warning("Drag-and-drop is disabled while you have an active search query.");
			},
			moveDragging(e, originalEvent)
			{
				if (this.searchQuery)
					return false;
			},
			endDragging(e)
			{
				if (this.searchQuery)
					return;
				for (let i = 0; i < this.filterSummaries.length; i++)
					this.filterSummaries[i].filter.MyOrder = i;

				let newOrderArg = this.getFilterOrder();
				let newOrderJson = JSON.stringify(newOrderArg);
				if (newOrderJson === this.filterOrderJson)
					return;

				ReorderFilters(this.projectName, newOrderArg)
					.then(data =>
					{
						if (data.success)
						{
							this.filterOrderJson = newOrderJson;
							toaster.success("Filter list reordered");
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			getFilterOrder()
			{
				return this.filterSummaries.map(f => ({
					FilterId: f.filter.FilterId,
					MyOrder: f.filter.MyOrder
				}));
			},
			runFilters()
			{
				ModalConfirmDialog("Please confirm you wish to run enabled filters against all events.", "Confirm")
					.then(result =>
					{
						if (result)
						{
							this.loading = true;
							RunEnabledFiltersAgainstAllEvents(this.projectName)
								.then(data =>
								{
									if (data.success)
										toaster.success("Filter execution complete");
									else
										toaster.error(data.error);
								})
								.catch(err =>
								{
									toaster.error(err);
								})
								.finally(() =>
								{
									this.loading = false;
								});
						}
					});
			},
			clearSearchQuery()
			{
				let query = Object.assign({}, this.$route.query);
				delete query.q;
				this.$router.replace({ name: this.$route.name, query });
			}
		},
		watch:
		{
			projectName()
			{
				this.loadFilterSummaries();
			},
			searchQuery()
			{
				this.loadFilterSummaries();
			},
			regexSearch()
			{
				if (this.searchQuery)
					this.loadFilterSummaries();
			}
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
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

	.loading
	{
		text-align: center;
		background-color: rgba(255,255,255,0.95);
		padding: 4px;
		border-radius: 4px;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
	}

	.tryAgain
	{
		margin-top: 10px;
	}

	.heading
	{
		font-size: 24px;
		font-weight: bold;
		color: #777777;
		margin-bottom: 0.65em;
	}

	.filterSearchDetailsContainer
	{
		margin-bottom: 1em;
	}

	.filterSummaryList
	{
		max-width: 600px;
		border: 1px solid black;
		border-radius: 5px;
		display: inline-block;
	}

	.filterNode
	{
		display: block;
		padding: 4px 8px;
		font-size: 18px;
		text-decoration: none;
		color: inherit;
		border-bottom: 1px solid #AAAAAA;
	}

		.filterNode:last-child
		{
			border-bottom: none;
		}

	.filterName
	{
	}

	.filterMeta
	{
		margin-left: 15px;
		color: #777777;
	}

		.filterMeta.filterEnabled
		{
			color: #00A000;
		}

		.filterMeta.filterDisabled
		{
			color: #CC0000;
		}

	.buttonBar
	{
		margin-top: 20px;
		border-top: 1px solid #AAAAAA;
		padding-top: 5px;
	}
</style>