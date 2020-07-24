<template>
	<div>
		<ControlBar :projectName="projectName" :onFilters="true" :filterId="filterId" :dirty="dirty" @commit="commitChanges" />
		<div class="pageRoot">
			<div v-if="error" class="error">
				{{error}}
				<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilter" /></div>
			</div>
			<div v-else-if="loading || filter">
				<div v-if="filter">
					<div class="inputSection properties">
						<label class="inputLabel">Filter Properties</label>
						<div>ID: {{filter.filter.FilterId}}</div>
						<div>
							<input type="text" v-model="filter.filter.Name" class="primaryInput" placeholder="Name" title="Name" /> <span class="hint">← Filter Name</span>
						</div>
						<div>
							<label><input type="checkbox" v-model="filter.filter.Enabled" /> Enabled (run automatically on new events)</label>
						</div>
						<div>
							<select v-model="filter.filter.ConditionHandling" class="primaryInput">
								<option value="All">All conditions must match</option>
								<option value="Any">At least one condition must match</option>
								<option value="Unconditional">Ignore conditions (always trigger actions)</option>
							</select>
						</div>
					</div>
					<div class="inputSection conditions">
						<label class="inputLabel">Conditions</label>
						<div class="conditionList">
							<FilterCondition v-for="condition in filter.conditions"
											 :key="condition.FilterConditionId"
											 :condition="condition"
											 @delete="deleteCondition"
											 class="condition" />
						</div>
						<div>
							<input type="button" value="New Condition (and Commit Changes)" @click="newCondition" />
						</div>
					</div>
					<div class="inputSection actions">
						<label class="inputLabel">Actions</label>
						<div class="actionList">
							<FilterAction v-for="action in filter.actions"
										  :key="action.FilterActionId"
										  :action="action"
										  :projectName="projectName"
										  @delete="deleteAction"
										  class="action" />
						</div>
						<div>
							<input type="button" value="New Action (and Commit Changes)" @click="newAction" />
						</div>
					</div>
					<div class="buttonBar">
						<input type="button" value="Commit Changes" @click="commitChanges" />
						<input type="button" value="Delete Filter" @click="beginDeleteFilter" />
						<input type="button" value="Run filter against all events" @click="runFilterAgainstAllEvents" />
					</div>
					<div class="notes">
						<div class="notesHeading">Filter Notes</div>
						<div class="notesBody">
							<ul>
								<li>When deleting <b>conditions</b> or <b>actions</b>, changes are committed for the entire <b>filter</b>.</li>
								<li>Disabled <b>filters</b> can still be run manually.</li>
								<li>Disabled <b>conditions</b> and actions are ignored (as if they did not exist) when executing the <b>filter</b>.</li>
								<li><code class="inline">All conditions must match</code> mode requires at least one <b>condition</b> to match.</li>
								<li>In <b>conditions</b>, the <code class="inline">Field Name</code> and <code class="inline">Query</code> fields are case-insensitive.</li>
								<li>A <b>condition's</b> <code class="inline">Field Name</code> field can be <code class="inline">EventType</code>, <code class="inline">SubType</code>, <code class="inline">Message</code>, <code class="inline">Date</code>, <code class="inline">Folder</code>, <code class="inline">Color</code>, or any application-specific tag key. An empty <code class="inline">Field Name</code> will fail the condition.</li>
								<li><b>Actions</b> are executed in the order they are defined above.</li>
								<li>The <b>action</b> <code class="inline">stop execution against matched event</code> prevents other <b>filters</b> and <b>actions</b> from executing against the matched event for the remainder of the current filtering operation.</li>
								<li>The <b>action</b> <code class="inline">move event to</code> requires an absolute folder path (e.g. <code class="inline">/ignored/spam</code>).  If the path is invalid or fails to resolve (case-insensitive), the event will not be moved.</li>
								<li><b>Actions</b> affect all users.</li>
							</ul>
						</div>
					</div>
				</div>
				<div v-if="loading" class="loadingOverlay">
					<div class="loading"><ScaleLoader /> Loading…</div>
				</div>
			</div>
			<div v-else>
				Failed to load filter {{filterId}} in project: {{projectName}}
				<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilter" /></div>
			</div>
		</div>
	</div>
</template>

<script>
	import { GetFilter, EditFilter, DeleteFilter, AddCondition, EditCondition, DeleteCondition, AddAction, EditAction, DeleteAction, RunFilterAgainstAllEvents } from 'appRoot/api/FilterData';
	import { TextInputDialog, ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';
	import { GetReadableTextColorHexForBackgroundColorHex } from 'appRoot/scripts/Util';
	import ControlBar from 'appRoot/vues/client/controls/ControlBar.vue';
	import FilterCondition from 'appRoot/vues/client/filters/FilterCondition.vue';
	import FilterAction from 'appRoot/vues/client/filters/FilterAction.vue';

	export default {
		components: { ControlBar, FilterCondition, FilterAction },
		props:
		{
			projectName: {
				type: String,
				required: true
			},
			filterId: {
				type: Number,
				required: true
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				loadingFilterId: null,
				originalFilterJson: null, // Serialized original filter object for change detection
				filter: null
			};
		},
		created()
		{
			this.loadFilter();
		},
		computed:
		{
			filterJson()
			{
				return JSON.stringify(this.filter);
			},
			dirty()
			{
				return this.originalFilterJson && (this.originalFilterJson !== this.filterJson);
			}
		},
		methods:
		{
			loadFilter()
			{
				if (this.loading)
					return;
				if (this.filterId === this.loadingFilterId)
					return;
				this.loading = true;
				this.loadingFilterId = this.filterId;
				this.error = null;

				GetFilter(this.projectName, this.loadingFilterId)
					.then(data =>
					{
						if (data.success)
						{
							this.originalFilterJson = JSON.stringify(data.filter);
							this.filter = data.filter;
							return data;
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
						if (this.loadingFilterId !== this.filterId)
							this.loadFilter();
						else
							this.loadingFilterId = null;
					});
			},
			hop(reloadAfter, promise) // handle async operation
			{
				this.loading = true;
				return promise
					.then(data =>
					{
						this.loading = false;
						if (data.success)
						{
							if (reloadAfter)
								return this.loadFilter();
							else
								return data;
						}
						else
							toaster.error(data.error);
						return new Promise((resolve, reject) => { });
					})
					.catch(err =>
					{
						toaster.error(err);
						this.loading = false;
					});
			},
			commitChanges()
			{
				this.hop(true, EditFilter(this.projectName, this.filter));
			},
			newCondition()
			{
				this.hop(false, EditFilter(this.projectName, this.filter))
					.then(data =>
					{
						//if (data)
						this.hop(true, AddCondition(this.projectName, { FilterId: this.filter.filter.FilterId }));
					});
			},
			newAction()
			{
				this.hop(false, EditFilter(this.projectName, this.filter))
					.then(() =>
					{
						//if (data)
						this.hop(true, AddAction(this.projectName, { FilterId: this.filter.filter.FilterId }));
					});
			},
			deleteCondition(condition)
			{
				ModalConfirmDialog("Delete this condition?", "Confirm")
					.then(result =>
					{
						if (result)
						{
							this.hop(false, EditFilter(this.projectName, this.filter))
								.then(() =>
								{
									this.hop(true, DeleteCondition(this.projectName, condition));
									//.then(() =>
									//{
									//	this.filter.conditions = this.filter.conditions.filter(c => c.FilterConditionId !== condition.FilterConditionId);
									//});
								});
						}
					});
			},
			deleteAction(action)
			{
				ModalConfirmDialog("Delete this action?", "Confirm")
					.then(result =>
					{
						if (result)
						{
							this.hop(false, EditFilter(this.projectName, this.filter))
								.then(() =>
								{
									this.hop(true, DeleteAction(this.projectName, action));
									//.then(() =>
									//{
									//	this.filter.actions = this.filter.conditions.filter(c => c.FilterActionId !== condition.FilterActionId);
									//});
								});
						}
					});
			},
			beginDeleteFilter()
			{
				ModalConfirmDialog("Delete this filter?", "Confirm")
					.then(result =>
					{
						if (result)
						{
							this.hop(false, DeleteFilter(this.projectName, this.filterId))
								.then(data =>
								{
									this.$router.replace({ name: 'clientFilters', query: { p: this.projectName } });
								});
						}
					});
			},
			runFilterAgainstAllEvents()
			{
				ModalConfirmDialog("Please confirm you wish to run this filter against all events.", "Confirm")
					.then(result =>
					{
						if (result)
						{
							this.hop(false, RunFilterAgainstAllEvents(this.projectName, this.filterId))
								.then(data =>
								{
									toaster.success("Filter execution complete");
								});
						}
					});
			}
		},
		watch:
		{
			projectName()
			{
				this.loadFilter();
			},
			filterId()
			{
				this.loadFilter();
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

	.inputSection
	{
		margin-bottom: 30px;
	}

		.inputSection .inputLabel
		{
			font-size: 24px;
			font-weight: bold;
			display: block;
			color: #555555;
			margin-bottom: 4px;
		}

		.inputSection .primaryInput
		{
			font-size: 16px;
			padding: 4px;
		}

		.inputSection.properties > *
		{
			margin-bottom: 10px;
		}

	.conditionList,
	.actionList
	{
		display: inline-block;
		border: 1px solid #777777;
		border-radius: 8px;
		overflow: hidden;
		min-height: 16px;
		min-width: 300px;
	}

	.conditionList
	{
		background-color: #dddde8;
	}

	.condition:nth-child(2n)
	{
		background-color: #b5b5d8;
	}

	.actionList
	{
		background-color: #dde8dd;
	}

	.action:nth-child(2n)
	{
		background-color: #b6d8b5;
	}

	.condition,
	.action
	{
		padding: 10px 5px 15px 5px;
		border-bottom: 1px solid #777777;
	}

		.condition:last-child,
		.action:last-child
		{
			border-bottom: none;
		}

	.hint
	{
		color: #777777;
		font-style: italic;
	}

	.buttonBar
	{
		margin-top: 10px;
		border-top: 1px solid #AAAAAA;
		padding-top: 5px;
	}

		.buttonBar > input
		{
			display: block;
			margin-bottom: 10px;
		}

	.notes
	{
		margin-top: 40px;
		border: 1px solid #777777;
		background-color: #F7F7F7;
	}

	.notesHeading
	{
		padding: 2px 4px;
		background-color: #E0E0E0;
		font-size: 18px;
		font-weight: bold;
	}

	.notesBody li
	{
		margin-bottom: 1em;
		line-height: 1.4em;
	}
</style>